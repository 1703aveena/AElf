using AElf.Common;
using AElf.Database;
using AElf.Kernel;
using AElf.Kernel.Infrastructure;
using AElf.Kernel.SmartContract.Application;
using AElf.Modularity;
using Google.Protobuf;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.Modularity;
using System.IO;
using AElf.Contracts.Genesis;
using AElf.Kernel.Consensus.Application;

namespace AElf.Contracts.TestBase
{
    [DependsOn(
        typeof(KernelAElfModule),
        typeof(DatabaseAElfModule)
    )]
    public class ContractTestAElfModule : AElfModule
    {
        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            context.Services.AddAssemblyOf<ContractTestAElfModule>();
            context.Services.AddKeyValueDbContext<BlockchainKeyValueDbContext>(o => o.UseInMemoryDatabase());
            context.Services.AddKeyValueDbContext<StateKeyValueDbContext>(o => o.UseInMemoryDatabase());
        }

        public override void OnPreApplicationInitialization(ApplicationInitializationContext context)
        {
            var contractZero = typeof(BasicContractZero);
            var code = File.ReadAllBytes(contractZero.Assembly.Location);
            var provider = context.ServiceProvider.GetService<IDefaultContractZeroCodeProvider>();
            provider.DefaultContractZeroRegistration = new SmartContractRegistration
            {
                Category = 2,
                Code = ByteString.CopyFrom(code),
                CodeHash = Hash.FromRawBytes(code)
            };
        }
    }
}