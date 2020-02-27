using AElf.Contracts.TestKit;
using AElf.Kernel.SmartContract.Application;
using AElf.Kernel.SmartContract.ExecutionPluginForMethodFee.FreeFeeTransactions;
using AElf.Kernel.SmartContract.ExecutionPluginForMethodFee.Tests;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Modularity;
using TokenContractChargeFeeStrategy = AElf.Kernel.SmartContract.ExecutionPluginForMethodFee.Tests.TokenContractChargeFeeStrategy;

namespace AElf.Kernel.SmartContract.ExecutionPluginForMethodCallThreshold.Tests
{
    [DependsOn(typeof(ContractTestModule),
        typeof(ExecutionPluginForMethodCallThresholdModule))]
    public class ExecutionPluginForMethodCallThresholdTestModule : ContractTestModule
    {
        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            Configure<ContractOptions>(o => o.ContractDeploymentAuthorityRequired = false );
            context.Services.AddSingleton<IPreExecutionPlugin, MethodCallingThresholdPreExecutionPlugin>();
            context.Services.AddSingleton<IChargeFeeStrategy, TokenContractChargeFeeStrategy>();
            context.Services.AddSingleton<ICalculateTxCostStrategy, TestCalculateTxStrategy>();
        }
    }
}