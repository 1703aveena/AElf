using System.Threading.Tasks;
using AElf.Kernel;
using Microsoft.Extensions.Options;
using Xunit;

namespace AElf.CrossChain.Communication.Grpc
{
    public sealed class GrpcCrossChainClientNodePluginTest : GrpcCrossChainClientTestBase
    {
        private readonly GrpcCrossChainServerNodePlugin _grpcCrossChainServerNodePlugin;
        private readonly GrpcCrossChainClientNodePlugin _grpcCrossChainClientNodePlugin;
        private readonly ChainOptions _chainOptions;
        private readonly GrpcCrossChainConfigOption _grpcCrossChainConfigOption;

        public GrpcCrossChainClientNodePluginTest()
        {
            _grpcCrossChainServerNodePlugin = GetRequiredService<GrpcCrossChainServerNodePlugin>();
            _grpcCrossChainClientNodePlugin = GetRequiredService<GrpcCrossChainClientNodePlugin>();
            _chainOptions = GetRequiredService<IOptionsSnapshot<ChainOptions>>().Value;
            _grpcCrossChainConfigOption = GetRequiredService<IOptionsSnapshot<GrpcCrossChainConfigOption>>().Value;
        }

        [Fact]
        public async Task ServerStartTest()
        {
            var chainId = _chainOptions.ChainId;
            await _grpcCrossChainServerNodePlugin.StartAsync(chainId);
        }

        [Fact]
        public async Task ClientStartTest()
        {
            var chainId = _chainOptions.ChainId;
            await _grpcCrossChainClientNodePlugin.StartAsync(chainId);
        }

        [Fact]
        public async Task ClientStartTest_Null()
        {
            var chainId = _chainOptions.ChainId;
            _grpcCrossChainConfigOption.RemoteParentChainServerPort = 0;
            await _grpcCrossChainClientNodePlugin.StartAsync(chainId);
        }

        [Fact]
        public async Task CreateClientTest()
        {
            var grpcCrossChainClientDto = new CrossChainClientDto()
            {
                RemoteChainId = ChainHelper.ConvertBase58ToChainId("AELF"),
                RemoteServerHost = _grpcCrossChainConfigOption.RemoteParentChainServerHost,
                RemoteServerPort = _grpcCrossChainConfigOption.RemoteParentChainServerPort
            };
            await _grpcCrossChainClientNodePlugin.CreateClientAsync(grpcCrossChainClientDto);
        }

        //TODO: Add test cases for GrpcCrossChainClientNodePlugin.ShutdownAsync after it is implemented [Case]

        [Fact]
        public async Task StopClientTest()
        {
            var chainId = ChainHelper.GetChainId(1);
            await _grpcCrossChainClientNodePlugin.StartAsync(chainId);
            await _grpcCrossChainClientNodePlugin.StopAsync();
        }

        public override void Dispose()
        {
            _grpcCrossChainServerNodePlugin?.StopAsync().Wait();
        }
    }
}