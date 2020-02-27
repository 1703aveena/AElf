using AElf.Kernel.Node.Infrastructure;
using AElf.Modularity;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Modularity;

namespace AElf.CrossChain.Communication.Grpc
{
    
    //TODO!! AElf.CrossChain.Communication -> AElf.CrossChain.Communication.Grpc -> AElf.CrossChain.Communication.Core
    //move grpc is the default implement of AElf.CrossChain.Communication package
    [DependsOn(typeof(CrossChainCommunicationModule))]
    public class GrpcCrossChainAElfModule : AElfModule
    {
        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            var services = context.Services;
            services.AddSingleton<IGrpcClientPlugin, GrpcCrossChainClientNodePlugin>();
            services.AddSingleton<IGrpcServePlugin, GrpcCrossChainServerNodePlugin>();
            services.AddSingleton<IGrpcCrossChainServer, GrpcCrossChainServer>();
            context.Services.AddTransient<INodePlugin, GrpcCrossChainNodePlugin>();
            var grpcCrossChainConfiguration = services.GetConfiguration().GetSection("CrossChain");
            Configure<GrpcCrossChainConfigOption>(grpcCrossChainConfiguration.GetSection("Grpc"));
        }
    }
}