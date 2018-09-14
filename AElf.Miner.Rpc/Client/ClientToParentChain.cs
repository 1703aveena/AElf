using AElf.Common.Attributes;
using AElf.Kernel;
using Grpc.Core;
using NLog;
namespace AElf.Miner.Rpc.Client
{
    [LoggerName("ClientToParentChain")]
    public class ClientToParentChain : ClientBase<ResponseParentChainBlockInfo>
    {
        private readonly ParentChainBlockInfoRpc.ParentChainBlockInfoRpcClient _client;

        public ClientToParentChain(Channel channel, ILogger logger, Hash targetChainId, int interval) 
            : base(logger, targetChainId, interval)
        {
            _client = new ParentChainBlockInfoRpc.ParentChainBlockInfoRpcClient(channel);
        }

        protected override AsyncDuplexStreamingCall<RequestBlockInfo, ResponseParentChainBlockInfo> Call()
        {
            return _client.Record();
        }
    }
}