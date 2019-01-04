using AElf.Kernel;
using AElf.Modularity;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Modularity;

namespace AElf.Contracts.Consensus.Tests
{
    [DependsOn(
        typeof(AElf.TestBase.Contract.ContractTestAElfModule)
    )]
    public class ConsensusContractTestAElfModule : AElfModule
    {
        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            context.Services.AddAssemblyOf<ConsensusContractTestAElfModule>();
        }
    }
}