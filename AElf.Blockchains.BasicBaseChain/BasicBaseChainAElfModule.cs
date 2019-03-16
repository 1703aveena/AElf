﻿using AElf.Common;
using AElf.Contracts.Consensus.DPoS;
using AElf.Contracts.Dividends;
using AElf.Contracts.Genesis;
using AElf.Contracts.Resource;
using AElf.Contracts.Resource.FeeReceiver;
using AElf.Contracts.Token;
using AElf.Kernel;
using AElf.Kernel.Consensus;
using AElf.Kernel.Consensus.DPoS;
using AElf.Kernel.SmartContract;
using AElf.Modularity;
using AElf.OS;
using AElf.OS.Node.Application;
using AElf.OS.Node.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.Modularity;
using Volo.Abp.Threading;

namespace AElf.Blockchains.BasicBaseChain
{
    [DependsOn(
        typeof(DPoSConsensusAElfModule),
        typeof(KernelAElfModule),
        typeof(OSAElfModule)
    )]
    public class BasicBaseChainAElfModule : AElfModule
    {
        public OsBlockchainNodeContext OsBlockchainNodeContext { get; set; }


        public override void PreConfigureServices(ServiceConfigurationContext context)
        {
        }

        public override void ConfigureServices(ServiceConfigurationContext context)
        {
        }

        public override void OnPreApplicationInitialization(ApplicationInitializationContext context)
        {
        }

        public override void OnApplicationInitialization(ApplicationInitializationContext context)
        {
            var chainOptions = context.ServiceProvider.GetService<IOptionsSnapshot<ChainOptions>>().Value;
            var dto = new OsBlockchainNodeContextStartDto()
            {
                ChainId = chainOptions.ChainId,
                ZeroSmartContract = typeof(BasicContractZero)
            };

            dto.InitializationSmartContracts.AddGenesisSmartContract<ConsensusContract>(
                ConsensusSmartContractAddressNameProvider.Name);
            dto.InitializationSmartContracts.AddGenesisSmartContract<TokenContract>(
                TokenSmartContractAddressNameProvider.Name);
            dto.InitializationSmartContracts.AddGenesisSmartContract<DividendsContract>(
                DividendsSmartContractAddressNameProvider.Name);
            dto.InitializationSmartContracts.AddGenesisSmartContract<ResourceContract>(
                ResourceSmartContractAddressNameProvider.Name);
            dto.InitializationSmartContracts.AddGenesisSmartContract<FeeReceiverContract>(
                ResourceFeeReceiverSmartContractAddressNameProvider.Name);

            var osService = context.ServiceProvider.GetService<IOsBlockchainNodeContextService>();
            var that = this;
            AsyncHelper.RunSync(async () => { that.OsBlockchainNodeContext = await osService.StartAsync(dto); });
        }
        
        public override void OnApplicationShutdown(ApplicationShutdownContext context)
        {
            var osService = context.ServiceProvider.GetService<IOsBlockchainNodeContextService>();
            var that = this;
            AsyncHelper.RunSync(() => osService.StopAsync(that.OsBlockchainNodeContext));
        }
    }
}