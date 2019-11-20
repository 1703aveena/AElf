using AElf.Contracts.TestKit;
using AElf.Kernel.SmartContract.Application;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Modularity;

namespace AElf.Kernel.SmartContract.ExecutionPluginForAcs1.Tests
{
    [DependsOn(typeof(ContractTestModule),
        typeof(ExecutionPluginForAcs1Module))]
    public class ExecutionPluginForAcs1TestModule : ContractTestModule
    {
        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            Configure<ContractOptions>(o => o.ContractDeploymentAuthorityRequired = false);
            context.Services.AddSingleton<IPreExecutionPlugin, FeeChargePreExecutionPlugin>();
        }
    }
}