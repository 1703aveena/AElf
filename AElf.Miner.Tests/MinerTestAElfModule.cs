using System.Threading.Tasks;
using AElf.Common;
using AElf.Crosschain;
using AElf.Database;
using AElf.Execution.Execution;
using AElf.Kernel;
using AElf.Kernel.Consensus;
using AElf.Kernel.Account;
using AElf.Kernel.Consensus;
using AElf.Kernel.Services;
using AElf.Kernel.Storages;
using AElf.Modularity;
using AElf.Runtime.CSharp;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Volo.Abp;
using Volo.Abp.Modularity;

namespace AElf.Miner.Tests
{
    [DependsOn(
        typeof(AElf.ChainController.ChainControllerAElfModule),
        typeof(AElf.SmartContract.SmartContractAElfModule),
        typeof(CSharpRuntimeAElfModule),
        typeof(AElf.TxPool.TxPoolAElfModule),
        typeof(ConsensusKernelAElfModule),
        typeof(CrossChainAElfModule),
        typeof(KernelAElfModule)
    )]
    public class MinerTestAElfModule : AElfModule
    {
        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            Configure<ChainOptions>(o => { o.ChainId = "AELF"; });
            
            context.Services.AddAssemblyOf<MinerTestAElfModule>();
            context.Services.AddScoped<IExecutingService, NoFeeSimpleExecutingService>();
            
            context.Services.AddKeyValueDbContext<BlockchainKeyValueDbContext>(o => o.UseInMemoryDatabase());
            context.Services.AddKeyValueDbContext<StateKeyValueDbContext>(o => o.UseInMemoryDatabase());
            context.Services.AddTransient<IAccountService>(o => Mock.Of<IAccountService>(
                c => c.GetAccountAsync() == Task.FromResult(Address.FromString("AELF_Test")) && c
                         .VerifySignatureAsync(It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<byte[]>()) ==
                     Task.FromResult(true)));
        }


        public override void OnPreApplicationInitialization(ApplicationInitializationContext context)
        {
        
        }
    }
}