﻿using AElf.ChainController;
using AElf.Common;
using AElf.Common.Application;
using AElf.Configuration;
using AElf.Configuration.Config.Chain;
using AElf.Miner.Miner;
using AElf.Miner.Rpc.Client;
using AElf.Miner.Rpc.Server;
using AElf.Miner.TxMemPool;
using AElf.Modularity;
using Google.Protobuf;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.Modularity;

namespace AElf.Miner
{
    [DependsOn(typeof(ChainControllerAElfModule))]
    public class MinerAElfModule : AElfModule
    {
        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            
            var minerConfig = MinerConfig.Default;
            if (NodeConfig.Instance.IsMiner)
            {
                minerConfig = new MinerConfig
                {
                    CoinBase = Address.Parse(NodeConfig.Instance.NodeAccount) 
                };
            }

            minerConfig.ChainId = ChainConfig.Instance.ChainId.ConvertBase58ToChainId();
            
            var services = context.Services;

            services.AddSingleton<IMinerConfig>(minerConfig);
            services.AddSingleton<ClientManager>();
            services.AddSingleton<ServerManager>();
        }

        public override void OnApplicationInitialization(ApplicationInitializationContext context)
        {
            
            //TODO! should define a interface like RuntimeEnvironment and inject it in ClientManager.
            context.ServiceProvider.GetService<ClientManager>()
                .Init(ApplicationHelpers.ConfigPath);
            context.ServiceProvider.GetService<ServerManager>()
                .Init(ApplicationHelpers.ConfigPath);
        }


    }
}