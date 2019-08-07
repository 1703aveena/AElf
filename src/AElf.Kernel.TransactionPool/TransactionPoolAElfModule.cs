using AElf.Kernel.TransactionPool.Application;
using AElf.Kernel.TransactionPool.Infrastructure;
using AElf.Modularity;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Modularity;

namespace AElf.Kernel.TransactionPool
{
    [DependsOn(typeof(CoreKernelAElfModule))]
    public class TransactionPoolAElfModule : AElfModule
    {
        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            var services = context.Services;
            services.AddSingleton<ITxHub, TxHub>();
            services.AddSingleton<ITransactionValidationService, TransactionValidationForTxHubService>();
            services.AddSingleton<ITransactionValidationForTxHubProvider, TransactionToAddressValidationForTxHubProvider>();
            services.AddSingleton<ITransactionValidationForTxHubProvider, TransactionFromAddressBalanceValidationForTxHubProvider>();

            context.Services.AddSingleton<IContractDeployDiscoveryService, ContractDeployDiscoveryService>();
            context.Services.AddSingleton<BestChainFoundEventHandler>();

            var configuration = context.Services.GetConfiguration();
            Configure<TransactionOptions>(configuration.GetSection("Transaction"));
        }
    }
}