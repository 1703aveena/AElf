using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using AElf.Kernel;
using AElf.OS.Network.Application;
using AElf.OS.Network.Infrastructure;
using AElf.OS.Network.Types;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;

namespace AElf.OS.Network.Grpc
{
    public class GrpcPeer : IPeer
    {
        private const int MaxMetricsPerMethod = 100;
        
        private const int AnnouncementTimeout = 600;
        private const int BlockRequestTimeout = 300;
        private const int TransactionSendTimeout = 300;
        private const int BlocksRequestTimeout = 500;

        private const int FinalizeConnectTimeout = 400;
        private const int UpdateHandshakeTimeout = 400;
        
        private enum MetricNames
        {
            Announce,
            GetBlocks,
            GetBlock,
            PreLibAnnounce,
            PreLibConfirm
        };
        
        private readonly Channel _channel;
        private readonly PeerService.PeerServiceClient _client;
        
        /// <summary>
        /// Property that describes a valid state. Valid here means that the peer is ready to be used for communication.
        /// </summary>
        public bool IsReady
        {
            get { return _channel.State == ChannelState.Idle || _channel.State == ChannelState.Ready; }
        }
        
        public long LastKnowLibHeight { get; private set; }

        public bool IsBest { get; set; }
        public Hash CurrentBlockHash { get; private set; }
        public long CurrentBlockHeight { get; private set; }
        
        public string PeerIpAddress { get; }
        public string PubKey { get; }
        public int ProtocolVersion { get; }
        public long ConnectionTime { get; }
        public bool Inbound { get; }
        public long StartHeight { get; }

        public IReadOnlyDictionary<long, AcceptedBlockInfo> RecentBlockHeightAndHashMappings { get; }
        private readonly ConcurrentDictionary<long, AcceptedBlockInfo> _recentBlockHeightAndHashMappings;
        

        public IReadOnlyDictionary<long, PreLibBlockInfo> PreLibBlockHeightAndHashMappings { get; }
        private readonly ConcurrentDictionary<long, PreLibBlockInfo> _preLibBlockHeightAndHashMappings;
       
        public bool CanStreamTransactions { get; private set; } = false;
        public bool CanStreamAnnounces { get; private set; } = false;
        
        public bool CanStreamPreLibAnnounces { get; private set; } = false;
        public bool CanStreamPreLibConfirmAnnounces { get; private set; } = false;
        
        public bool CanStreamBlocks { get; private set; }
        
        public IReadOnlyDictionary<string, ConcurrentQueue<RequestMetric>> RecentRequestsRoundtripTimes { get; }
        private readonly ConcurrentDictionary<string, ConcurrentQueue<RequestMetric>> _recentRequestsRoundtripTimes;
        
        private AsyncClientStreamingCall<Transaction, VoidReply> _transactionStreamCall;
        private AsyncClientStreamingCall<PeerNewBlockAnnouncement, VoidReply> _announcementStreamCall;
        private AsyncClientStreamingCall<PeerPreLibAnnouncement, VoidReply> _preLibAnnounceStreamCall;
        private AsyncClientStreamingCall<PeerPreLibConfirmAnnouncement, VoidReply> _preLibConfirmAnnounceStreamCall; 
        private AsyncDuplexStreamingCall<BlockRequest, BlockReply> _blockRequestBlockStream;

        public GrpcPeer(Channel channel, PeerService.PeerServiceClient client, GrpcPeerInfo peerInfo)
        {
            _channel = channel;
            _client = client;

            PeerIpAddress = peerInfo.PeerIpAddress;
            PubKey = peerInfo.PublicKey;
            ProtocolVersion = peerInfo.ProtocolVersion;
            ConnectionTime = peerInfo.ConnectionTime;
            Inbound = peerInfo.IsInbound;
            StartHeight = peerInfo.StartHeight;
            LastKnowLibHeight = peerInfo.LibHeightAtHandshake;

            _recentBlockHeightAndHashMappings = new ConcurrentDictionary<long, AcceptedBlockInfo>();
            RecentBlockHeightAndHashMappings = new ReadOnlyDictionary<long, AcceptedBlockInfo>(_recentBlockHeightAndHashMappings);
            
            _preLibBlockHeightAndHashMappings = new ConcurrentDictionary<long, PreLibBlockInfo>();
            PreLibBlockHeightAndHashMappings = new ReadOnlyDictionary<long, PreLibBlockInfo>(_preLibBlockHeightAndHashMappings);
            
            _recentRequestsRoundtripTimes = new ConcurrentDictionary<string, ConcurrentQueue<RequestMetric>>();
            RecentRequestsRoundtripTimes =
                new ReadOnlyDictionary<string, ConcurrentQueue<RequestMetric>>(_recentRequestsRoundtripTimes);

            _recentRequestsRoundtripTimes.TryAdd(nameof(MetricNames.Announce), new ConcurrentQueue<RequestMetric>());
            _recentRequestsRoundtripTimes.TryAdd(nameof(MetricNames.GetBlock), new ConcurrentQueue<RequestMetric>());
            _recentRequestsRoundtripTimes.TryAdd(nameof(MetricNames.GetBlocks), new ConcurrentQueue<RequestMetric>());
            _recentRequestsRoundtripTimes.TryAdd(nameof(MetricNames.PreLibAnnounce),
                new ConcurrentQueue<RequestMetric>());
            _recentRequestsRoundtripTimes.TryAdd(nameof(MetricNames.PreLibConfirm),
                new ConcurrentQueue<RequestMetric>());
        }
        
        public Dictionary<string, List<RequestMetric>> GetRequestMetrics()
        {
            Dictionary<string, List<RequestMetric>> metrics = new Dictionary<string, List<RequestMetric>>();

            foreach (var roundtripTime in _recentRequestsRoundtripTimes.ToArray())
            {
                var metricsToAdd = new List<RequestMetric>();
                
                metrics.Add(roundtripTime.Key, metricsToAdd);
                foreach (var requestMetric in roundtripTime.Value)
                {
                    metricsToAdd.Add(requestMetric);
                }
            }

            return metrics;
        }
        
        public async Task UpdateHandshakeAsync()
        {
            GrpcRequest request = new GrpcRequest
            {
                ErrorMessage = $"Error while updating handshake."
            };
            
            Metadata data = new Metadata
            {
                {GrpcConstants.TimeoutMetadataKey, UpdateHandshakeTimeout.ToString()}
            };
            
            var handshake = await RequestAsync(_client, c => c.UpdateHandshakeAsync(new UpdateHandshakeRequest(), data), request);
             
            if (handshake != null)
                LastKnowLibHeight = handshake.LibBlockHeight;
        }

        public async Task FinalizeConnectAsync()
        {
            GrpcRequest request = new GrpcRequest { ErrorMessage = $"Error while finalizing request to {this}." };
            Metadata data = new Metadata { {GrpcConstants.TimeoutMetadataKey, FinalizeConnectTimeout.ToString()} };

            await RequestAsync(_client, c => c.FinalizeConnectAsync(new Handshake(), data), request);
        }

        private async Task<BlockWithTransactions> RequestBlockUnaryAsync(Hash hash)
        {
            var blockRequest = new BlockRequest {Hash = hash};

            GrpcRequest request = new GrpcRequest
            {
                ErrorMessage = $"Block request for {hash} failed.",
                MetricName = nameof(MetricNames.GetBlock),
                MetricInfo = $"Block request for {hash}"
            };

            Metadata data = new Metadata { {GrpcConstants.TimeoutMetadataKey, BlockRequestTimeout.ToString()} };

            var blockReply = await RequestAsync(_client, c => c.RequestBlockAsync(blockRequest, data), request);

            return blockReply?.Block;
        }

        public async Task<List<BlockWithTransactions>> GetBlocksAsync(Hash firstHash, int count)
        {
            var blockRequest = new BlocksRequest {PreviousBlockHash = firstHash, Count = count};
            var blockInfo = $"{{ first: {firstHash}, count: {count} }}";
            
            GrpcRequest request = new GrpcRequest
            {
                ErrorMessage = $"Get blocks for {blockInfo} failed.",
                MetricName = nameof(MetricNames.GetBlocks),
                MetricInfo = $"Get blocks for {blockInfo}"
            };

            Metadata data = new Metadata { {GrpcConstants.TimeoutMetadataKey, BlocksRequestTimeout.ToString()} };

            var list = await RequestAsync(_client, c => c.RequestBlocksAsync(blockRequest, data), request);

            if (list == null)
                return new List<BlockWithTransactions>();

            return list.Blocks.ToList();
        }

        #region Streaming
        
        public void StartBlockRequestStreaming()
        { 
            _blockRequestBlockStream = _client.RequestBlockStream();
            CanStreamBlocks = true;
        }
        
        public async Task<BlockWithTransactions> RequestBlockAsync(Hash blockHash)
        {
            if (!CanStreamBlocks)
            {
                return await RequestBlockUnaryAsync(blockHash);
            }
            
            try
            {
                Stopwatch s = Stopwatch.StartNew();
                
                await _blockRequestBlockStream.RequestStream.WriteAsync(new BlockRequest { Hash = blockHash }).ConfigureAwait(false);
                bool received = await _blockRequestBlockStream.ResponseStream.MoveNext().ConfigureAwait(false);
                
                s.Stop();
                
                if (received)
                {
                    if (s.ElapsedMilliseconds > BlockRequestTimeout)
                        throw new NetworkException($"Block request for slow: {s.ElapsedMilliseconds}, hash {blockHash}");
                    
                    var block = _blockRequestBlockStream.ResponseStream.Current;
                    return block.Block;
                }
            }
            catch (RpcException e)
            {
                if (!CanStreamBlocks) // Already down
                    return null;
                
                CanStreamBlocks = false;
                _blockRequestBlockStream.Dispose();
                
                throw new NetworkException($"Failed stream to {this}: ", e, NetworkExceptionType.BlockRequestStream);
            }
            
            return null;
        }

        public void StartAnnouncementStreaming()
        {
            _announcementStreamCall = _client.AnnouncementBroadcastStream();
            CanStreamAnnouncements = true;
        }
        
        public void StartPreLibAnnouncementStreaming()
        {
            _preLibAnnounceStreamCall = _client.PreLibAnnounceStream();
            CanStreamPreLibAnnounces = true;
        }
        
        public void StartPreLibConfirmAnnouncementStreaming()
        {
            _preLibConfirmAnnounceStreamCall = _client.PreLibConfirmAnnounceStream();
            CanStreamPreLibConfirmAnnounces = true;
        }
        
        public async Task AnnounceAsync(PeerNewBlockAnnouncement header)
        {
            if (!CanStreamAnnouncements)
            {
                // if we cannot stream we use the unary version of the send.
                await UnaryAnnounceAsync(header);
                return;
            }
            
            try
            {
                await _announcementStreamCall.RequestStream.WriteAsync(header);
            }
            catch (RpcException e)
            {
                if (!CanStreamAnnouncements) // Already down
                    return;
                
                CanStreamAnnouncements = false;
                _announcementStreamCall.Dispose();
                
                throw new NetworkException($"Failed stream to {this}: ", e, NetworkExceptionType.AnnounceStream);
            }
            catch (Exception e)
            {
                throw new NetworkException($"Failed stream to {this}: ", e);
            }
        }

        public void StartTransactionStreaming()
        {
            _transactionStreamCall = _client.TransactionBroadcastStream();
            CanStreamTransactions = true;
        }
        
        public async Task PreLibAnnounceAsync(PeerPreLibAnnouncement peerPreLibAnnouncement)
        {
            if (!CanStreamPreLibAnnounces)
            {
                // if we cannot stream we use the unary version of the send.
                await UnaryPreLibAnnounceAsync(peerPreLibAnnouncement);
                Logger.LogDebug("Not streaming pre lib announce.");
                return;
            }

            try
            {
                await _preLibAnnounceStreamCall.RequestStream.WriteAsync(peerPreLibAnnouncement);
            }
            catch (RpcException e)
            {
                if (!CanStreamPreLibAnnounces) // Already down
                    return;
                
                CanStreamPreLibAnnounces = false;
                _preLibAnnounceStreamCall.Dispose();
                
                throw new NetworkException($"Failed stream to {this}: ", e, NetworkExceptionType.PreLibAnnounceStream);
            }
            catch (Exception e)
            {
                throw new NetworkException($"Failed stream to {this}: ", e);
            }
        }

        public async Task PreLibConfirmAnnounceAsync(PeerPreLibConfirmAnnouncement peerPreLibConfirmAnnouncement)
        { 
            if (!CanStreamPreLibConfirmAnnounces)
            {
                // if we cannot stream we use the unary version of the send.
                await UnaryPreLibConfirmAnnounceAsync(peerPreLibConfirmAnnouncement);
                Logger.LogDebug("Not streaming pre lib confirm announce.");
                return;
            }
            
            try
            {
                await _preLibConfirmAnnounceStreamCall.RequestStream.WriteAsync(peerPreLibConfirmAnnouncement);
            }
            catch (RpcException e)
            {
                if (!CanStreamPreLibConfirmAnnounces) // Already down
                    return;
                
                CanStreamPreLibConfirmAnnounces = false;
                _preLibConfirmAnnounceStreamCall.Dispose();
                
                throw new NetworkException($"Failed stream to {this}: ", e, NetworkExceptionType.PreLibConfirmAnnounceStream);
            }
            catch (Exception e)
            {
                throw new NetworkException($"Failed stream to {this}: ", e);
            }
        }

        public async Task SendTransactionAsync(Transaction tx)
        {
            if (!CanStreamTransactions)
            {
                // if we cannot stream we use the unary version of the send.
                await UnarySendTransactionAsync(tx);
                return;
            }
            
            try
            {
                await _transactionStreamCall.RequestStream.WriteAsync(tx);
            }
            catch (RpcException e)
            {
                if (!CanStreamTransactions) // Already down
                    return;
                
                CanStreamTransactions = false;
                _transactionStreamCall.Dispose();
                
                throw new NetworkException($"Failed stream to {this}: ", e, NetworkExceptionType.TransactionStream);
            }
            catch (Exception e)
            {
                throw new NetworkException($"Failed stream to {this}: ", e);
            }
        }

        #endregion
        
        public Task UnarySendTransactionAsync(Transaction tx)
        {
            var request = new GrpcRequest { ErrorMessage = $"Broadcast transaction for {tx.GetHash()} failed." };
            var data = new Metadata {{ GrpcConstants.TimeoutMetadataKey, TransactionSendTimeout.ToString() }};
            
            return RequestAsync(_client, c => c.SendTransactionAsync(tx, data), request);
        }
        
        public Task UnaryAnnounceAsync(PeerNewBlockAnnouncement header)
        {
            GrpcRequest request = new GrpcRequest
            {
                ErrorMessage = $"Broadcast announce for {header.BlockHash} failed.",
                MetricName = nameof(MetricNames.Announce),
                MetricInfo = $"Block hash {header.BlockHash}"
            };

            Metadata data = new Metadata { {GrpcConstants.TimeoutMetadataKey, AnnouncementTimeout.ToString()} };

            return RequestAsync(_client, c => c.AnnounceAsync(header, data), request);
        }
        
        public Task UnaryPreLibAnnounceAsync(PeerPreLibAnnouncement preLibAnnouncement)
        {
            GrpcRequest request = new GrpcRequest
            {
                ErrorMessage = $"Broadcast pre lib announce for {preLibAnnouncement.BlockHash} failed.",
                MetricName = nameof(MetricNames.PreLibAnnounce),
                MetricInfo = $"Block hash {preLibAnnouncement.BlockHash}"
            };

            Metadata data = new Metadata { {GrpcConstants.TimeoutMetadataKey, AnnouncementTimeout.ToString()} };

            return RequestAsync(_client, c => c.PreLibAnnounceAsync(preLibAnnouncement, data), request);
        }
        
        public Task UnaryPreLibConfirmAnnounceAsync(PeerPreLibConfirmAnnouncement preLibConfirmAnnouncement)
        {
            GrpcRequest request = new GrpcRequest
            {
                ErrorMessage = $"Broadcast pre lib confirm announce for {preLibConfirmAnnouncement.BlockHash} failed.",
                MetricName = nameof(MetricNames.PreLibConfirm),
                MetricInfo = $"Block hash {preLibConfirmAnnouncement.BlockHash}"
            };

            Metadata data = new Metadata { {GrpcConstants.TimeoutMetadataKey, AnnouncementTimeout.ToString()} };

            return RequestAsync(_client, c => c.PreLibConfirmAnnounceAsync(preLibConfirmAnnouncement, data), request);
        }

        private async Task<TResp> RequestAsync<TResp>(PeerService.PeerServiceClient client,
            Func<PeerService.PeerServiceClient, AsyncUnaryCall<TResp>> func, GrpcRequest requestParams)
        {
            var metricsName = requestParams.MetricName;
            bool timeRequest = !string.IsNullOrEmpty(metricsName);
            var requestStartTime = TimestampHelper.GetUtcNow();
            
            Stopwatch requestTimer = null;
            
            if (timeRequest)
                requestTimer = Stopwatch.StartNew();
                
            try
            {
                var response = await func(client);

                if (timeRequest)
                {
                    requestTimer.Stop();
                    RecordMetric(requestParams, requestStartTime, requestTimer.ElapsedMilliseconds);
                }
                
                return response;
            }
            catch (AggregateException e)
            {
                HandleFailure(e.Flatten(), requestParams.ErrorMessage);
            }
            finally
            {
                if (timeRequest)
                {
                    requestTimer.Stop();
                    RecordMetric(requestParams, requestStartTime, requestTimer.ElapsedMilliseconds);
                }
            }

            return default(TResp);
        }

        private void RecordMetric(GrpcRequest grpcRequest, Timestamp requestStartTime, long elapsedMilliseconds)
        {
            var metrics = _recentRequestsRoundtripTimes[grpcRequest.MetricName];
                    
            while (metrics.Count >= MaxMetricsPerMethod)
                metrics.TryDequeue(out _);
                    
            metrics.Enqueue(new RequestMetric
            {
                Info = grpcRequest.MetricInfo,
                RequestTime = requestStartTime,
                MethodName = grpcRequest.MetricName,
                RoundTripTime = elapsedMilliseconds
            });
        }

        /// <summary>
        /// This method handles the case where the peer is potentially down. If the Rpc call
        /// put the channel in TransientFailure or Connecting, we give the connection a certain time to recover.
        /// </summary>
        private void HandleFailure(AggregateException exceptions, string errorMessage)
        {
            // If channel has been shutdown (unrecoverable state) remove it.
            string message = $"Failed request to {this}: {errorMessage}";
            NetworkExceptionType type = NetworkExceptionType.Rpc;
            
            if (_channel.State == ChannelState.Shutdown)
            {
                message = $"Peer is shutdown - {this}: {errorMessage}";
                type = NetworkExceptionType.Unrecoverable;
            }
            else if (_channel.State == ChannelState.TransientFailure || _channel.State == ChannelState.Connecting)
            {
                message = $"Failed request to {this}: {errorMessage}";
                type = NetworkExceptionType.PeerUnstable;
            }
            else if (exceptions.InnerException is RpcException rpcEx && rpcEx.StatusCode == StatusCode.Cancelled)
            {
                message = $"Failed request to {this}: {errorMessage}";
                type = NetworkExceptionType.Unrecoverable;
            }
            
            throw new NetworkException(message, exceptions, type);
        }

        public async Task<bool> TryWaitForStateChangedAsync()
        {
            await _channel.TryWaitForStateChangedAsync(_channel.State,
                DateTime.UtcNow.AddSeconds(NetworkConstants.DefaultPeerDialTimeoutInMilliSeconds));

            // Either we connected again or the state change wait timed out.
            if (_channel.State == ChannelState.TransientFailure || _channel.State == ChannelState.Connecting)
                return false;

            return true;
        }

        public async Task StopAsync()
        {
            try
            {
                await _channel.ShutdownAsync();
            }
            catch (InvalidOperationException)
            {
                // If channel already shutdown
            }
        }

        public void HandlerRemoteAnnounce(PeerNewBlockAnnouncement peerNewBlockAnnouncement)
        {
            CurrentBlockHeight = peerNewBlockAnnouncement.BlockHeight;
            CurrentBlockHash = peerNewBlockAnnouncement.BlockHash;
            if (_recentBlockHeightAndHashMappings.TryGetValue(CurrentBlockHeight, out var blockInfo))
            {
                if (peerNewBlockAnnouncement.HasFork || blockInfo.BlockHash != CurrentBlockHash)
                {
                    blockInfo.HasFork = true;
                }
            }
            else
            {
                blockInfo = new AcceptedBlockInfo
                {
                    BlockHash = CurrentBlockHash,
                    HasFork = false
                };
            }
            
            _recentBlockHeightAndHashMappings[CurrentBlockHeight] = blockInfo;
            while (_recentBlockHeightAndHashMappings.Count > 40)
            {
                _recentBlockHeightAndHashMappings.TryRemove(_recentBlockHeightAndHashMappings.Keys.Min(), out _);
            }
        }

        public void HandlerRemotePreLibAnnounce(PeerPreLibAnnouncement peerPreLibAnnouncement)
        {
            var blockHeight = peerPreLibAnnouncement.BlockHeight;
            var blockHash = peerPreLibAnnouncement.BlockHash;
            var preLibCount = peerPreLibAnnouncement.PreLibCount;
            if (_preLibBlockHeightAndHashMappings.TryGetValue(blockHeight, out var preLibBlockInfo))
            {
                if (preLibBlockInfo.BlockHash != blockHash)
                    return;
                if(preLibCount > preLibBlockInfo.PreLibCount)
                    preLibBlockInfo.PreLibCount = preLibCount;
            }
            else
            {
                preLibBlockInfo = new PreLibBlockInfo
                {
                    BlockHash = blockHash,
                    PreLibCount = preLibCount
                };
            }

            _preLibBlockHeightAndHashMappings[blockHeight] = preLibBlockInfo;
            while (_preLibBlockHeightAndHashMappings.Count > 40)
            {
                _preLibBlockHeightAndHashMappings.TryRemove(_preLibBlockHeightAndHashMappings.Keys.Min(), out _);
            }
        }

        public bool HasBlock(long blockHeight, Hash blockHash)
        {
            return _recentBlockHeightAndHashMappings.TryGetValue(blockHeight, out var blockInfo) &&
                   blockInfo.BlockHash == blockHash && !blockInfo.HasFork;
        }

        public bool HasPreLib(long blockHeight, Hash blockHash)
        {
            return _preLibBlockHeightAndHashMappings.TryGetValue(blockHeight, out var preLibBlockInfo) &&
                preLibBlockInfo.BlockHash == blockHash;
        }

        public async Task SendDisconnectAsync()
        {
            await _client.DisconnectAsync(new DisconnectReason {Why = DisconnectReason.Types.Reason.Shutdown});
        }

        public override string ToString()
        {
            return $"{{ listening-port: {PeerIpAddress}, key: {PubKey.Substring(0, 45)}... }}";
        }
    }
}