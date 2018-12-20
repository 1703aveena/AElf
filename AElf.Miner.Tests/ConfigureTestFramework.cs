﻿using AElf.ChainController;
using AElf.Common;
using AElf.Configuration;
using AElf.Configuration.Config.Chain;
using AElf.Database;
using AElf.Execution;
using AElf.Execution.Scheduling;
using AElf.Kernel;
using AElf.Kernel.Managers;
using AElf.Kernel.Storages;
using AElf.Kernel.Types.Transaction;
using AElf.Miner.TxMemPool;
using AElf.Runtime.CSharp;
using AElf.SmartContract;
using AElf.Synchronization.BlockSynchronization;
using Autofac;
using Autofac.Core;
using Xunit;
using Xunit.Abstractions;
using Xunit.Frameworks.Autofac;

[assembly: TestFramework("AElf.Miner.Tests.ConfigureTestFramework", "AElf.Miner.Tests")]

namespace AElf.Miner.Tests
{
    public class ConfigureTestFramework : AutofacTestFramework
    {
        public ConfigureTestFramework(IMessageSink diagnosticMessageSink)
            : base(diagnosticMessageSink)
        {
        }

        protected override void ConfigureContainer(ContainerBuilder builder)
        {
            ChainConfig.Instance.ChainId = Hash.LoadByteArray(new byte[] { 0x01, 0x02, 0x03 }).DumpBase58();
            NodeConfig.Instance.NodeAccount = Address.Generate().GetFormatted();

            builder.RegisterModule(new LoggerAutofacModule());
            builder.RegisterModule(new DatabaseAutofacModule());
            builder.RegisterModule(new KernelAutofacModule());
            builder.RegisterType<DataStore>().As<IDataStore>();
            builder.RegisterType<BlockValidationService>().As<IBlockValidationService>().SingleInstance();
            builder.RegisterType<ChainContextService>().As<IChainContextService>().SingleInstance();
            builder.RegisterType<ChainService>().As<IChainService>().SingleInstance();
            builder.RegisterType<BlockSet>().As<IBlockSet>().SingleInstance();
            builder.RegisterType<ChainManagerBasic>().As<IChainManagerBasic>().SingleInstance();
            builder.RegisterType<BlockManagerBasic>().As<IBlockManagerBasic>().SingleInstance();
            builder.RegisterType<TransactionManager>().As<ITransactionManager>().SingleInstance();
            builder.RegisterType<TransactionTraceManager>().As<ITransactionTraceManager>().SingleInstance();
            builder.RegisterType<StateStore>().As<IStateStore>();
            builder.RegisterType<TxSignatureVerifier>().As<ITxSignatureVerifier>();
            builder.RegisterType<TxRefBlockValidator>().As<ITxRefBlockValidator>();
        }
    }
}