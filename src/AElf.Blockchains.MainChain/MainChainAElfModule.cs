﻿using AElf.Blockchains.BasicBaseChain;
using AElf.Blockchains.ContractInitialization;
using AElf.Modularity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Volo.Abp.Modularity;

namespace AElf.Blockchains.MainChain
{
    [DependsOn(
        typeof(BasicBaseChainAElfModule),
        typeof(MainChainContractInitializationAElfModule)
    )]
    public class MainChainAElfModule : AElfModule
    {
        public ILogger<MainChainAElfModule> Logger { get; set; }
        
        public MainChainAElfModule()
        {
            Logger = NullLogger<MainChainAElfModule>.Instance;
        }
    }
}