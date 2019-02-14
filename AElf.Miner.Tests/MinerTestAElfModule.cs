using System.Threading.Tasks;
using AElf.Common;
using AElf.Configuration;
using AElf.Configuration.Config.Chain;
using AElf.Database;
using AElf.Execution.Execution;
using AElf.Kernel;
using AElf.Kernel.Account;
using AElf.Kernel.Consensus;
using AElf.Kernel.Execution;
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
        typeof(AElf.Miner.Rpc.MinerRpcAElfModule),
        typeof(KernelAElfModule)
    )]
    public class MinerTestAElfModule : AElfModule
    {
        public override void ConfigureServices(ServiceConfigurationContext context)
        {

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
            ChainConfig.Instance.ChainId = Hash.LoadByteArray(new byte[] {0x01, 0x02, 0x03}).DumpBase58();
        }

    }
}