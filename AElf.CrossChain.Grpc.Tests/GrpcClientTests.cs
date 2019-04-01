using System.Threading.Tasks;
using AElf.CrossChain.Cache;
using AElf.Cryptography.Certificate;
using Grpc.Core;
using Shouldly;
using Xunit;

namespace AElf.CrossChain.Grpc
{
    public class GrpcClientTests : GrpcCrossChainClientTestBase
    {
        private const string Host = "localhost";
        private const int ListenPort = 2000;
        private GrpcClientForParentChain parentClient;
        private GrpcClientForSideChain sideClient;
        
        private ICrossChainServer _server;
        private ICertificateStore _certificateStore;
        private ICrossChainDataProducer _crossChainDataProducer;
        
        public GrpcClientTests()
        {
            _server = GetRequiredService<ICrossChainServer>();
            _certificateStore = GetRequiredService<ICertificateStore>();
            _crossChainDataProducer = GetRequiredService<ICrossChainDataProducer>();
            
            InitServerAndClient();      
        }

        [Fact]
        public async Task StartIndexingRequest()
        {
            //var result = await parentClient.StartIndexingRequest(0, _crossChainDataProducer);
        }

        [Fact]
        public async Task ParentChainClient_TryHandShakeAsync()
        {
            var result = await parentClient.TryHandShakeAsync(0, 2000);
            result.Result.ShouldBeTrue();

            parentClient = new GrpcClientForParentChain("localhost:3000", "test", 0);
            await Assert.ThrowsAsync<RpcException>(()=>parentClient.TryHandShakeAsync(0, 3000));
        }
        
        [Fact]
        public async Task SideChainClient_TryHandShakeAsync()
        {
            var result = await sideClient.TryHandShakeAsync(0, 2000);
            result.Result.ShouldBeTrue();

            sideClient = new GrpcClientForSideChain("localhost:3000", "test");
            await Assert.ThrowsAsync<RpcException>(()=>sideClient.TryHandShakeAsync(0, 3000));
        }

        private void InitServerAndClient()
        {
            var keyStore = _certificateStore.LoadKeyStore("test");
            var cert = _certificateStore.LoadCertificate("test");
            var keyCert = new KeyCertificatePair(cert, keyStore);

            _server.StartAsync(Host, 2000, keyCert).Wait();
            
            string uri = $"{Host}:{ListenPort}";
            parentClient = new GrpcClientForParentChain(uri, cert, 0);
            sideClient = new GrpcClientForSideChain(uri, cert);
        }
    }
}