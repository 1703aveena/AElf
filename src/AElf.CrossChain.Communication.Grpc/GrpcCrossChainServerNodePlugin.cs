using System.Threading.Tasks;
using AElf.Kernel.Node.Infrastructure;
using Microsoft.Extensions.Options;

namespace AElf.CrossChain.Communication.Grpc
{
    public class GrpcCrossChainServerNodePlugin : IGrpcCrossChainPlugin
    {
        private readonly GrpcCrossChainConfigOption _grpcCrossChainConfigOption;
        private readonly IGrpcCrossChainServer _grpcCrossChainServer;

        public GrpcCrossChainServerNodePlugin(IOptionsSnapshot<GrpcCrossChainConfigOption> grpcCrossChainConfigOption, 
            IGrpcCrossChainServer grpcCrossChainServer)
        {
            _grpcCrossChainConfigOption = grpcCrossChainConfigOption.Value;
            _grpcCrossChainServer = grpcCrossChainServer;
        }

        public Task StartAsync(int chainId)
        {
            if (string.IsNullOrEmpty(_grpcCrossChainConfigOption.LocalServerHost) 
                || _grpcCrossChainConfigOption.LocalServerPort == 0)
                return Task.CompletedTask;
            return _grpcCrossChainServer.StartAsync(_grpcCrossChainConfigOption.LocalServerHost,
                _grpcCrossChainConfigOption.LocalServerPort);
        }

        public Task StopAsync()
        {
            _grpcCrossChainServer.Dispose();
            return Task.CompletedTask;
        }
    }
}