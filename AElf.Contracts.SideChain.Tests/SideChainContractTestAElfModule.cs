using AElf.Kernel;
using AElf.Modularity;
using AElf.Contracts.TestBase;
using AElf.Runtime.CSharp;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Modularity;

namespace AElf.Contracts.SideChain.Tests
{
    [DependsOn(
        typeof(AElf.ChainController.ChainControllerAElfModule),
        typeof(AElf.SmartContract.SmartContractAElfModule),
        typeof(CSharpRuntimeAElfModule),
        typeof(TestBase.ContractTestAElfModule),
        typeof(KernelAElfModule)
    )]
    public class SideChainContractTestAElfModule : AElfModule
    {
        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            context.Services.AddAssemblyOf<SideChainContractTestAElfModule>();
        }
    }
}