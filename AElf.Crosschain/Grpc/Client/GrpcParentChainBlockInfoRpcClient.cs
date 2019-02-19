using System;
using System.Threading;
using Grpc.Core;

namespace AElf.Crosschain.Grpc.Client
{
    public class GrpcParentChainBlockInfoRpcClient : GrpcCrossChainClient<ResponseParentChainBlockData>
    {
        private readonly CrossChainRpc.CrossChainRpcClient _client;

        public GrpcParentChainBlockInfoRpcClient(Channel channel, GrpcClientBase grpcClientBase) : base(channel, grpcClientBase)
        {
            _client = new CrossChainRpc.CrossChainRpcClient(channel);
        }

        protected override AsyncDuplexStreamingCall<RequestCrossChainBlockData, ResponseParentChainBlockData> Call(int milliSeconds = 0)
        {
            return milliSeconds == 0
                ? _client.RequestParentChainDuplexStreaming()
                : _client.RequestParentChainDuplexStreaming(deadline: DateTime.UtcNow.AddMilliseconds(milliSeconds));
        }

        protected override AsyncServerStreamingCall<ResponseParentChainBlockData> Call(RequestCrossChainBlockData requestCrossChainBlockData)
        {
            return _client.RequestParentChainServerStreaming(requestCrossChainBlockData);
        }
    }
}