﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using AElf.Common.Attributes;
using AElf.Common.ByteArrayHelpers;
using AElf.Common.Collections;
using AElf.Kernel;
using AElf.Network.Config;
using AElf.Network.Connection;
using AElf.Network.Data;
using AElf.Network.Peers.Exceptions;
using Google.Protobuf;
using NLog;

[assembly:InternalsVisibleTo("AElf.Network.Tests")]
namespace AElf.Network.Peers
{
    public class PeerAddedEventArgs : EventArgs
    {
        public IPeer Peer { get; set; }
    }
    
    public class PeerRemovedEventArgs : EventArgs
    {
        public IPeer Peer { get; set; }
    }

    public class NetMessageReceivedArgs : EventArgs
    {
        public TimeoutRequest Request { get; set; }
        public Message Message { get; set; }
        public PeerMessageReceivedArgs PeerMessage { get; set; }
    }

    public class RequestFailedArgs : EventArgs
    {
        public Message RequestMessage { get; set; }
        
        public byte[] ItemHash { get; set; }
        public int BlockIndex { get; set; }
        
        public List<IPeer> TriedPeers = new List<IPeer>();
    }
    
    [LoggerName(nameof(NetworkManager))]
    public class NetworkManager : INetworkManager
    {
        public const int DefaultMaxBlockHistory = 15;
        
        public const int DefaultRequestTimeout = 1000;
        public const int DefaultRequestMaxRetry = TimeoutRequest.DefaultMaxRetry;
        
        public event EventHandler MessageReceived;
        public event EventHandler RequestFailed;
        
        private readonly IAElfNetworkConfig _networkConfig;
        private readonly IPeerManager _peerManager;
        private readonly ILogger _logger;
        
        // List of non bootnode peers
        private readonly List<IPeer> _peers = new List<IPeer>();

        public int RequestTimeout { get; set; } = DefaultRequestTimeout;
        public int RequestMaxRetry { get; set; } = DefaultRequestMaxRetry;

        private Object _pendingRequestsLock = new Object();  
        public List<TimeoutRequest> _pendingRequests;

        private BoundedByteArrayQueue _lastBlocksReceived;
        public int MaxBlockHistory { get; set; } = DefaultMaxBlockHistory;

        private BlockingPriorityQueue<PeerMessageReceivedArgs> _incomingJobs;

        public NetworkManager(IAElfNetworkConfig config, IPeerManager peerManager, ILogger logger)
        {
            _incomingJobs = new BlockingPriorityQueue<PeerMessageReceivedArgs>();
            _pendingRequests = new List<TimeoutRequest>();
            
            _networkConfig = config;
            _peerManager = peerManager;
            _logger = logger;
            
            // todo peerManager.PeerAdded
        }

        internal TimeoutRequest HasRequestWithHash(byte[] hash)
        {
            lock (_pendingRequestsLock)
            {
                return _pendingRequests.FirstOrDefault(r => r.ItemHash.BytesEqual(hash));
            }
        }
        
        internal TimeoutRequest HasRequestWithIndex(int index)
        {
            lock (_pendingRequestsLock)
            {
                return _pendingRequests.FirstOrDefault(r => r.BlockIndex == index);
            }
        }

        /// <summary>
        /// This method start the server that listens for incoming
        /// connections and sets up the manager.
        /// </summary>
        public void Start()
        {
            // init the queue
            _lastBlocksReceived = new BoundedByteArrayQueue(MaxBlockHistory);
            
            //todo _peerManager.PeerAdded 
            _peerManager.Start();
            
            Task.Run(() => StartProcessingIncoming()).ConfigureAwait(false);
        }
        
        #region Message processing

        private void StartProcessingIncoming()
        {
            while (true)
            {
                try
                {
                    PeerMessageReceivedArgs msg = _incomingJobs.Take();
                    ProcessPeerMessage(msg);
                }
                catch (Exception e)
                {
                    _logger?.Trace(e, "Error while processing incoming messages");
                }
            }
        }
        
        private void ProcessPeerMessage(PeerMessageReceivedArgs args)
        {
            TimeoutRequest originalRequest = null;
                
            if (args.Message.Type == (int) MessageType.Tx)
            {
                originalRequest = HandleTransactionMessage(args.Peer, args.Message);
            }
            else if (args.Message.Type == (int) MessageType.Block)
            {
                originalRequest = HandleBlockMessage(args.Peer, args.Message);
            }

            if (args.Message.Type == (int) MessageType.BroadcastBlock)
            {
                Block b = Block.Parser.ParseFrom(args.Message.Payload); // todo later deserializations will be redundant
                byte[] blockHash = b.GetHash().Value.ToByteArray();
                    
                if (_lastBlocksReceived.Contains(blockHash))
                    return;
                    
                _lastBlocksReceived.Enqueue(blockHash);
                    
                foreach (var peer in _peers.Where(p => !p.Equals(args.Peer)))
                {
                    try 
                    {
                        peer.EnqueueOutgoing(args.Message); 
                    }
                    catch (Exception ex) { } // todo think about removing this try/catch, enqueue should be fire and forget
                }
            }
                
            var evt = new NetMessageReceivedArgs {
                Message = args.Message,
                PeerMessage = args,
                Request = originalRequest
            };

            // raise the event so the higher levels can process it.
            MessageReceived?.Invoke(this, evt);
        }
        
        private void HandleNewMessage(object sender, EventArgs e)
        {
            if (e is PeerMessageReceivedArgs args)
            {
                _incomingJobs.Enqueue(args, 0);
            }
        }

        #endregion
        
        public void QueueTransactionRequest(byte[] transactionHash, IPeer hint)
        {
            try
            {
                IPeer selectedPeer = hint ?? _peers.FirstOrDefault();
            
                if(selectedPeer == null)
                    return;
            
                TxRequest br = new TxRequest { TxHash = ByteString.CopyFrom(transactionHash) };
                var msg = NetRequestFactory.CreateMessage(MessageType.TxRequest, br.ToByteArray());
            
                // Select peer for request
                TimeoutRequest request = new TimeoutRequest(transactionHash, msg, RequestTimeout);
                request.MaxRetryCount = RequestMaxRetry;
            
                lock (_pendingRequestsLock)
                {
                    _pendingRequests.Add(request);
                }
            
                request.RequestTimedOut += RequestOnRequestTimedOut;
                request.TryPeer(selectedPeer);
                
                _logger?.Trace($"Request for transaction {transactionHash?.ToHex()} send to {selectedPeer}");
            }
            catch (Exception e)
            {
                _logger?.Trace(e, $"Error while requesting transaction {transactionHash?.ToHex()}.");
            }
        }

        /// <summary>
        /// Callback called when the requests internal timer has executed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="eventArgs"></param>
        private void RequestOnRequestTimedOut(object sender, EventArgs eventArgs)
        {
            if (sender == null)
            {
                _logger?.Trace("Request timeout - sender null.");
                return;
            }

            if (sender is TimeoutRequest req)
            {
                string id = req.ItemHash != null ? $"for transaction with hash {req.ItemHash.ToHex()}" : $"for block with index {req.BlockIndex}";
                _logger?.Trace("Request timedout " + id + $", with {req.Peer} and timeout : {TimeSpan.FromMilliseconds(req.Timeout)}.");
                
                if (req.HasReachedMaxRetry)
                {
                    lock (_pendingRequestsLock)
                    {
                        _pendingRequests.Remove(req);
                    }
                    
                    req.RequestTimedOut -= RequestOnRequestTimedOut;
                    FireRequestFailed(req);
                    return;
                }
                
                IPeer nextPeer = _peers.FirstOrDefault(p => !p.Equals(req.Peer));
                if (nextPeer != null)
                {
                    _logger?.Trace("Trying another peer " + id + $", next : {nextPeer}.");
                    req.TryPeer(nextPeer);
                }
            }
            else
            {
                _logger?.Trace("Request timeout - sender wrong type.");
            }
        }

        private void FireRequestFailed(TimeoutRequest req)
        {
            RequestFailedArgs reqFailedArgs = new RequestFailedArgs
            {
                RequestMessage = req.RequestMessage,
                BlockIndex = req.BlockIndex,
                ItemHash = req.ItemHash,
                TriedPeers = req.TriedPeers.ToList()
            };

            string id = req.ItemHash != null ? $"for transaction with hash {req.ItemHash.ToHex()}" : $"for block with index {req.BlockIndex}";
            _logger?.Trace("Request failed " + id + $" after {req.TriedPeers.Count} tries. Max tries : {req.MaxRetryCount}.");
                    
            RequestFailed?.Invoke(this, reqFailedArgs);
        }

        public void QueueBlockRequestByIndex(int index)
        {
            try
            {
                Peer selectedPeer = (Peer)_peers.FirstOrDefault();
            
                if(selectedPeer == null)
                    return;
            
                BlockRequest br = new BlockRequest { Height = index };
                Message message = NetRequestFactory.CreateMessage(MessageType.RequestBlock, br.ToByteArray()); 
            
                // Select peer for request
                TimeoutRequest request = new TimeoutRequest(index, message, RequestTimeout);
                request.MaxRetryCount = RequestMaxRetry;
                
                lock (_pendingRequestsLock)
                {
                    _pendingRequests.Add(request);
                }

                request.TryPeer(selectedPeer);
                _logger?.Trace($"Request for block at index {index}");
            }
            catch (Exception e)
            {
                _logger?.Trace(e, $"Error while requesting block for index {index}.");
            }
        }

        /// <summary>
        /// Returns the first occurence of the peer. IPeer
        /// implementations may override the equality logic.
        /// </summary>
        /// <param name="peer"></param>
        /// <returns></returns>
        public IPeer GetPeer(IPeer peer)
        {
            return _peers?.FirstOrDefault(p => p.Equals(peer));
        }
        
        /// <summary>
        /// Callback for when a Peer fires a <see cref="PeerDisconnected"/> event. It unsubscribes
        /// the manager from the events and removes it from the list.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ProcessClientDisconnection(object sender, EventArgs e)
        {
            if (sender != null && e is PeerDisconnectedArgs args && args.Peer != null)
            {
                IPeer peer = args.Peer;
                
                peer.MessageReceived -= HandleNewMessage;
                peer.PeerDisconnected -= ProcessClientDisconnection;
                
                //RemovePeer(args.Peer);
                // todo
            }
        }

        internal TimeoutRequest HandleBlockMessage(Peer peer, Message msg)
        {
            if (peer == null || msg == null)
            {
                _logger?.Trace("HandleBlockMessage : peer or message null.");
                return null;
            }
            
            try
            {
                Block block = Block.Parser.ParseFrom(msg.Payload);

                if (block?.Header == null)
                    return null;

                TimeoutRequest request;
                lock (_pendingRequestsLock)
                {
                    request = _pendingRequests.FirstOrDefault(r => r.BlockIndex == (int)block.Header.Index);
                }

                if (request != null)
                {
                    request.RequestTimedOut -= RequestOnRequestTimedOut;
                    request.Stop();
                    
                    lock (_pendingRequestsLock)
                    {
                        _pendingRequests.Remove(request);
                    }
                    
                    _logger?.Trace($"Block request matched and removed. Index : { block.Header.Index }");
                }
                else
                {
                    _logger?.Trace($"Block request not found. Index : { block.Header.Index }");
                }

                return request;
            }
            catch (Exception e)
            {
                _logger?.Trace(e, "HandleBlockMessage : exception while handling block.");
                return null;
            }
        }

        internal TimeoutRequest HandleTransactionMessage(Peer peer, Message msg)
        {
            if (peer == null || msg == null)
            {
                _logger?.Trace("HandleTransactionMessage : peer or message null.");
                return null;
            }
            
            try
            {
                Transaction tx = Transaction.Parser.ParseFrom(msg.Payload);
                byte[] txHash = tx.GetHash().Value.ToByteArray();

                TimeoutRequest request;
                lock (_pendingRequestsLock)
                {
                    request = _pendingRequests.FirstOrDefault(r => r.ItemHash.BytesEqual(txHash));
                }

                if (request != null)
                {
                    request.RequestTimedOut -= RequestOnRequestTimedOut;
                    request.Stop();

                    lock (_pendingRequestsLock)
                    {
                        _pendingRequests.Remove(request);
                    }
                    
                    _logger?.Trace($"Transaction request matched and removed. Hash : {txHash.ToHex()}");
                }
                else
                {
                    _logger?.Trace($"Transaction request not found. Hash : {txHash.ToHex()}");
                }

                return request;
            }
            catch (Exception e)
            {
                _logger?.Trace(e, "HandleTransactionMessage : exception while handling transaction.");
                return null;
            }
        }

        public async Task<int> BroadcastBock(byte[] hash, byte[] payload)
        {
            _lastBlocksReceived.Enqueue(hash);
            return await BroadcastMessage(MessageType.BroadcastBlock, payload);
        }
        
        /// <summary>
        /// This message broadcasts data to all of its peers. This creates and
        /// sends a <see cref="AElfPacketData"/> object with the provided pay-
        /// load and message type.
        /// </summary>
        /// <param name="messageType"></param>
        /// <param name="payload"></param>
        /// <param name="messageId"></param>
        /// <returns></returns>
        public async Task<int> BroadcastMessage(MessageType messageType, byte[] payload)
        {
            try
            {
                
                Message packet = NetRequestFactory.CreateMessage(messageType, payload);
                return BroadcastMessage(packet);
            }
            catch (Exception e)
            {
                _logger?.Error(e, "Error while sending a message to the peers.");
                return 0;
            }
        }

        public int BroadcastMessage(Message message)
        {
            if (_peers == null || !_peers.Any())
                return 0;

            int count = 0;
            
            try
            {
                foreach (var peer in _peers)
                {
                    try
                    {
                        peer.EnqueueOutgoing(message); //todo
                        count++;
                    }
                    catch (Exception e) { }
                }
            }
            catch (Exception e)
            {
                _logger?.Error(e, "Error while sending a message to the peers.");
            }

            return count;
        }
    }
}