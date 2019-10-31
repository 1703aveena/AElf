using AElf.Kernel;
using AElf.Kernel.Consensus.AEDPoS;
using AElf.Kernel.SmartContract.Application;
using AElf.Kernel.TransactionPool.Infrastructure;
using AElf.Modularity;
using AElf.Runtime.CSharp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Volo.Abp.Modularity;

namespace AElf.OS
{
    [DependsOn(
        typeof(CoreOSAElfModule),
        typeof(AEDPoSAElfModule),
        typeof(CSharpRuntimeAElfModule),
        typeof(KernelTestAElfModule)
    )]
    public class OSTestBaseAElfModule : AElfModule
    {
        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            context.Services.AddSingleton<OSTestHelper>();
        }
    }
}