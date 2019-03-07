﻿using AElf.Kernel.ChainController;
using AElf.Kernel.Consensus;
using AElf.Kernel.Node;
using AElf.Kernel.SmartContract;
using AElf.Kernel.SmartContractExecution;
using AElf.Kernel.TransactionPool;
using AElf.Modularity;
using Volo.Abp.Modularity;

namespace AElf.Kernel
{
    [DependsOn(
        typeof(CoreKernelAElfModule), 
        typeof(ChainControllerAElfModule), 
        typeof(SmartContractAElfModule),
        typeof(NodeAElfModule),
        typeof(SmartContractExecutionAElfModule),
        typeof(ConsensusAElfModule),
        typeof(TransactionPoolAElfModule))]
    public class KernelAElfModule : AElfModule<KernelAElfModule>
    {

    }
}