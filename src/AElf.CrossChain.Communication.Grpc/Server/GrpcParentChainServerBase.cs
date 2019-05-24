using System.Threading.Tasks;
using Acs7;
using AElf.CrossChain.Communication.Application;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Local;

namespace AElf.CrossChain.Communication.Grpc
{
    public class GrpcParentChainServerBase : ParentChainRpc.ParentChainRpcBase, ITransientDependency
    {
        public ILogger<GrpcParentChainServerBase> Logger { get; set; }
        private readonly ICrossChainResponseService _crossChainResponseService;

        public GrpcParentChainServerBase(ICrossChainResponseService crossChainResponseService)
        {
            _crossChainResponseService = crossChainResponseService;
        }

        public override async Task RequestIndexingFromParentChainAsync(CrossChainRequest crossChainRequest, 
            IServerStreamWriter<ParentChainBlockData> responseStream, ServerCallContext context)
        {
            Logger.LogTrace(
                $"Parent Chain Server received IndexedInfo message from chain {ChainHelpers.ConvertChainIdToBase58(crossChainRequest.FromChainId)}.");
            var requestedHeight = crossChainRequest.NextHeight;
            var remoteChainId = crossChainRequest.FromChainId;
            while (requestedHeight - crossChainRequest.NextHeight <= 64)
            {
                var parentChainBlockData =
                    await _crossChainResponseService.ResponseParentChainBlockDataAsync(requestedHeight, remoteChainId);
                if (parentChainBlockData == null)
                    break;
                await responseStream.WriteAsync(parentChainBlockData);
                requestedHeight++;
            }
            
//            PublishCrossChainRequestReceivedEvent(context.Host, crossChainRequest.ListeningPort, crossChainRequest.FromChainId);
        }
        
        
//        public override Task<HandShakeReply> CrossChainHandShake(HandShake request, ServerCallContext context)
//        {
//            Logger.LogTrace($"Received shake from chain {ChainHelpers.ConvertChainIdToBase58(request.FromChainId)}.");
////            PublishCrossChainRequestReceivedEvent(context.Host, request.ListeningPort, request.FromChainId);
//            return Task.FromResult(new HandShakeReply {Result = true});
//        }
//
        public override async Task<ChainInitializationData> RequestChainInitializationDataFromParentChainAsync(SideChainInitializationRequest request, ServerCallContext context)
        {
            Logger.LogTrace(
                $"Received initialization data request from  chain {ChainHelpers.ConvertChainIdToBase58(request.ChainId)}");
            var sideChainInitializationResponse =
                await _crossChainResponseService.ResponseChainInitializationDataFromParentChainAsync(request.ChainId);
            return sideChainInitializationResponse;
        }
    }
}