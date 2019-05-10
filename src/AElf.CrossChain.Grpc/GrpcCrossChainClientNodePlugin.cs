using System.Threading.Tasks;
using AElf.Contracts.CrossChain;
using AElf.Kernel;
using AElf.Kernel.Blockchain.Application;
using AElf.Kernel.Blockchain.Events;
using Google.Protobuf;
using Microsoft.Extensions.Options;
using Volo.Abp.EventBus;

namespace AElf.CrossChain.Grpc
{
    public class GrpcCrossChainClientNodePlugin : IChainInitializationPlugin, ILocalEventHandler<GrpcCrossChainRequestReceivedEvent>, ILocalEventHandler<CrossChainDataValidatedEvent>
    {
        private readonly GrpcClientProvider _grpcClientProvider;
        private readonly GrpcCrossChainConfigOption _grpcCrossChainConfigOption;
        private readonly CrossChainConfigOption _crossChainConfigOption;
        private readonly INewChainRegistrationService _newChainRegistrationService;
        private readonly IBlockchainService _blockchainService;
        private bool _readyToLaunchClient;
        private int _localChainId;
        
        public GrpcCrossChainClientNodePlugin(GrpcClientProvider grpcClientProvider, 
            IOptionsSnapshot<GrpcCrossChainConfigOption> grpcCrossChainConfigOption, 
            IOptionsSnapshot<CrossChainConfigOption> crossChainConfigOption, 
            INewChainRegistrationService newChainRegistrationService, IBlockchainService blockchainService)
        {
            _grpcClientProvider = grpcClientProvider;
            _newChainRegistrationService = newChainRegistrationService;
            _blockchainService = blockchainService;
            _grpcCrossChainConfigOption = grpcCrossChainConfigOption.Value;
            _crossChainConfigOption = crossChainConfigOption.Value;
        }

        public async Task StartAsync(int chainId)
        {
            _localChainId = chainId;
            var libIdHeight = await _blockchainService.GetLibHashAndHeight();
            
            if (libIdHeight.BlockHeight > Constants.GenesisBlockHeight)
            {
                // start cache if the lib is higher than genesis 
                await _newChainRegistrationService.RegisterNewChainsAsync(libIdHeight.BlockHash, libIdHeight.BlockHeight);
            }
            
            if (string.IsNullOrEmpty(_grpcCrossChainConfigOption.RemoteParentChainNodeIp) 
                || _grpcCrossChainConfigOption.LocalServerPort == 0) 
                return;
            
            await _grpcClientProvider.CreateOrUpdateClient(new GrpcCrossChainCommunicationDto
            {
                RemoteChainId = _crossChainConfigOption.ParentChainId,
                RemoteIp = _grpcCrossChainConfigOption.RemoteParentChainNodeIp,
                RemotePort = _grpcCrossChainConfigOption.RemoteParentChainNodePort,
                LocalChainId = chainId,
                LocalListeningPort = _grpcCrossChainConfigOption.LocalServerPort,
                ConnectionTimeout = _grpcCrossChainConfigOption.ConnectionTimeout
            }, true);
        }

        public async Task HandleEventAsync(GrpcCrossChainRequestReceivedEvent requestReceivedEventData)
        {
            if (!await IsReadyToRequest())
                return;
            var grpcCrossChainCommunicationDto = new GrpcCrossChainCommunicationDto
            {
                ConnectionTimeout = _grpcCrossChainConfigOption.ConnectionTimeout,
                LocalListeningPort = _grpcCrossChainConfigOption.LocalServerPort,
                RemoteIp = requestReceivedEventData.RemoteIp,
                RemotePort = requestReceivedEventData.RemotePort,
                RemoteChainId = requestReceivedEventData.RemoteChainId,
                LocalChainId = _localChainId
            };

            await _grpcClientProvider.CreateOrUpdateClient(grpcCrossChainCommunicationDto,
                requestReceivedEventData.RemoteChainId == _crossChainConfigOption.ParentChainId);
        }
        
        public async Task HandleEventAsync(CrossChainDataValidatedEvent eventData)
        {
            if (!await IsReadyToRequest())
                return;
            _grpcClientProvider.RequestCrossChainIndexing(_grpcCrossChainConfigOption.LocalServerPort);
        }
        
        public async Task ShutdownAsync()
        {
            await _grpcClientProvider.CloseClients();
        }

        public async Task<SideChainInitializationResponse> RequestChainInitializationContextAsync(int chainId)
        {
            string uri = string.Join(":", _grpcCrossChainConfigOption.RemoteParentChainNodeIp, _grpcCrossChainConfigOption.RemoteParentChainNodePort);
            var chainInitializationContext = await _grpcClientProvider.RequestChainInitializationContextAsync(uri, chainId, _grpcCrossChainConfigOption.ConnectionTimeout);
            return chainInitializationContext;
        }

        private async Task<bool> IsReadyToRequest()
        {
            if (!_readyToLaunchClient)
            {
                var libIdHeight = await _blockchainService.GetLibHashAndHeight();
                _readyToLaunchClient = libIdHeight.BlockHeight > Constants.GenesisBlockHeight;
            }

            return _readyToLaunchClient;
        }
    }
}