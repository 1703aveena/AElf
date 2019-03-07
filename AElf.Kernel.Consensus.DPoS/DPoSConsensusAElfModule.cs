using AElf.Kernel.Blockchain.Application;
using AElf.Kernel.Consensus.Application;
using AElf.Kernel.Consensus.DPoS.Application;
using AElf.Kernel.Consensus.Infrastructure;
using AElf.Kernel.Consensus.Scheduler.FluentScheduler;
using AElf.Kernel.Consensus.Scheduler.RxNet;
using AElf.Kernel.Miner.Application;
using AElf.Kernel.SmartContract.Application;
using AElf.Modularity;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Modularity;

namespace AElf.Kernel.Consensus.DPoS
{
    [DependsOn(typeof(RxNetSchedulerAElfModule))]
    // ReSharper disable once InconsistentNaming
    public class DPoSConsensusAElfModule : AElfModule
    {
        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            context.Services.AddSingleton<IConsensusService, ConsensusService>();

            context.Services.AddScoped<ISmartContractAddressNameProvider, ConsensusSmartContractAddressNameProvider>();

            context.Services.AddScoped<IBlockExtraDataProvider, ConsensusExtraDataProvider>();

            context.Services.AddSingleton<ConsensusControlInformation>();
            
            context.Services.AddAssemblyOf<DPoSConsensusAElfModule>();
            
            context.Services.AddSingleton<BestChainFoundEventHandler>();

            context.Services.AddScoped<ISystemTransactionGenerator, ConsensusTransactionGenerator>();
            context.Services.AddSingleton<IConsensusInformationGenerationService, DPoSInformationGenerationService>();
            context.Services.AddTransient<IBlockValidationProvider, DPoSConsensusValidationProvider>();
        }
    }
}