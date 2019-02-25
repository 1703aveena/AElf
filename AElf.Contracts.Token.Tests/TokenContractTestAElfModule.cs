using AElf.Contracts.TestBase;
using AElf.Kernel;
using AElf.Modularity;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Modularity;

namespace AElf.Contracts.Token
{
    [DependsOn(
        typeof(KernelAElfModule),
        typeof(ContractTestAElfModule)
    )]
    public class TokenContractTestAElfModule : AElfModule
    {
        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            context.Services.AddAssemblyOf<TokenContractTestAElfModule>();
        }
    }
}