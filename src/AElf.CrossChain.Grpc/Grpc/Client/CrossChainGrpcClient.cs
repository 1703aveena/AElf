using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AElf.Contracts.CrossChain;
using AElf.CrossChain.Cache;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace AElf.CrossChain.Grpc
{
    public abstract class CrossChainGrpcClient
    {
        protected readonly Channel Channel;
        protected readonly int DialTimeout;
        private readonly int _localChainId;
        public string Target => Channel.Target;
        private readonly CrossChainRpc.CrossChainRpcClient _grpcClient;

        protected CrossChainGrpcClient(string uri, int localChainId, int dialTimeout)
        {
            _localChainId = localChainId;
            DialTimeout = dialTimeout;
            Channel = CreateChannel(uri);
            _grpcClient = new CrossChainRpc.CrossChainRpcClient(Channel);
        }
        
        /// <summary>
        /// Create a new channel
        /// </summary>
        /// <param name="uriStr"></param>
        /// <param name="crt">Certificate</param>
        /// <returns></returns>
        private Channel CreateChannel(string uriStr, string crt)
        {
            var channelCredentials = new SslCredentials(crt);
            var channel = new Channel(uriStr, channelCredentials);
            return channel;
        }

        private Channel CreateChannel(string uriStr)
        {
            return new Channel(uriStr, ChannelCredentials.Insecure);
        }
        
        public async Task Close()
        {
            await Channel.ShutdownAsync();
        }
        
        public async Task StartIndexingRequest(int chainId, long targetHeight,
            ICrossChainDataProducer crossChainDataProducer, int localListeningPort)
        {
            var requestData = new CrossChainRequest
            {
                FromChainId = _localChainId,
                NextHeight = targetHeight,
                ListeningPort = localListeningPort
            };

            using (var serverStream = RequestIndexing(requestData))
            {
                while (await serverStream.ResponseStream.MoveNext())
                {
                    var response = serverStream.ResponseStream.Current;

                    // requestCrossChain failed or useless response
                    if (!crossChainDataProducer.TryAddBlockCacheEntity(new BlockCacheEntity {ChainId = response.BlockData.ChainId, Height = response.BlockData.Height, Payload = response.BlockData.Payload}))
                    {
                        break;
                    }

                    crossChainDataProducer.Logger.LogTrace(
                        $"Received response from chain {ChainHelpers.ConvertChainIdToBase58(response.BlockData.ChainId)} at height {response.BlockData.Height}");
                }
            }
        }
    
        public Task<HandShakeReply> HandShakeAsync(int chainId, int localListeningPort)
        {
            var handShakeReply = _grpcClient.CrossChainIndexingShake(new HandShake
            {
                FromChainId = chainId,
                ListeningPort = localListeningPort
            }, new CallOptions().WithDeadline(DateTime.UtcNow.AddSeconds(DialTimeout)));
            return Task.FromResult(handShakeReply);
        }
        
        public Task<SideChainInitializationResponse> RequestChainInitializationContext(int chainId)
        {
            var sideChainInitializationResponse = _grpcClient.RequestChainInitializationContextFromParentChain(
                new SideChainInitializationRequest
                {
                    ChainId = chainId
                }, new CallOptions().WithDeadline(DateTime.UtcNow.AddSeconds(DialTimeout)));
            return Task.FromResult(sideChainInitializationResponse);
        }

        protected abstract AsyncServerStreamingCall<CrossChainResponse> RequestIndexing(
            CrossChainRequest crossChainRequest);
    }
    
    public class GrpcClientForSideChain : CrossChainGrpcClient
    {
        public GrpcClientForSideChain(string uri, int localChainId, int dialTimeout) : base(uri, localChainId, dialTimeout)
        {
        }

        protected override AsyncServerStreamingCall<CrossChainResponse> RequestIndexing(CrossChainRequest crossChainRequest)
        {
            return new CrossChainRpc.CrossChainRpcClient(Channel).RequestIndexingFromSideChain(crossChainRequest,
                new CallOptions().WithDeadline(DateTime.UtcNow.AddSeconds(DialTimeout)));
        }
    }
    
    public class GrpcClientForParentChain : CrossChainGrpcClient
    {
        public GrpcClientForParentChain(string uri, int localChainId, int dialTimeout) : base(uri, localChainId, dialTimeout)
        {
        }

        protected override AsyncServerStreamingCall<CrossChainResponse> RequestIndexing(CrossChainRequest crossChainRequest)
        {
            return new CrossChainRpc.CrossChainRpcClient(Channel).RequestIndexingFromParentChain(crossChainRequest,
                new CallOptions().WithDeadline(DateTime.UtcNow.AddSeconds(DialTimeout)));
        }
    }
    
//    public abstract class CrossChainGrpcClient
//    {
//        protected readonly Channel Channel;
//        protected readonly int LocalChainId;
//        protected readonly int DialTimeout;
//
//        protected CrossChainGrpcClient(string uri, int localChainId, int dialTimeout)
//        {
//            LocalChainId = localChainId;
//            DialTimeout = dialTimeout;
//            Channel = CreateChannel(uri);
//        }
//        
//        public abstract Task<IndexingHandShakeReply> HandShakeAsync(int chainId, int localListeningPort);
//        public abstract Task StartIndexingRequest(int chainId, long targetHeight, ICrossChainDataProducer crossChainDataProducer);
//        public abstract Task<ChainInitializationContext> RequestChainInitializationContext(int chainId);
//
//        /// <summary>
//        /// Create a new channel
//        /// </summary>
//        /// <param name="uriStr"></param>
//        /// <param name="crt">Certificate</param>
//        /// <returns></returns>
//        private Channel CreateChannel(string uriStr, string crt)
//        {
//            var channelCredentials = new SslCredentials(crt);
//            var channel = new Channel(uriStr, channelCredentials);
//            return channel;
//        }
//
//        private Channel CreateChannel(string uriStr)
//        {
//            return new Channel(uriStr, ChannelCredentials.Insecure);
//        }
//        public async Task Close()
//        {
//            await Channel.ShutdownAsync();
//        }
//    }
}