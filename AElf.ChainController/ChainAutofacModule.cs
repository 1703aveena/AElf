﻿using AElf.ChainController.TxMemPool;
using AElf.ChainController.TxMemPoolBM;
using AElf.Common.Application;
using Autofac;

namespace AElf.ChainController
{
    public class ChainAutofacModule: Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            var assembly = typeof(BlockValidationService).Assembly;
            builder.RegisterAssemblyTypes(assembly).AsImplementedInterfaces();
            builder.RegisterType<ContractTxPool>().As<IContractTxPool>().SingleInstance();
            builder.RegisterType<TxPoolServiceBM>().As<ITxPoolService>().SingleInstance();
            builder.RegisterType<ChainCreationService>().As<IChainCreationService>();
            builder.RegisterType<ChainContextService>().As<IChainContextService>();
            builder.RegisterType<TransactionResultService>().As<ITransactionResultService>();
            builder.RegisterType<AccountContextService>().As<IAccountContextService>().SingleInstance();
            builder.RegisterType<ChainService>().As<IChainService>().SingleInstance();
        }
    }
}