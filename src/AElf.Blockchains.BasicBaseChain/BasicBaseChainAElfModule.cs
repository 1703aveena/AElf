﻿using System;
using AElf.CrossChain.Grpc;
using AElf.Kernel;
using AElf.Kernel.Consensus.DPoS;
using AElf.Modularity;
using AElf.OS;
using AElf.OS.Network.Grpc;
using AElf.OS.Rpc.ChainController;
using AElf.OS.Rpc.Net;
using AElf.OS.Rpc.Wallet;
using AElf.Runtime.CSharp;
using AElf.RuntimeSetup;
using AElf.WebApp.Web;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.AspNetCore;
using Volo.Abp.Modularity;

namespace AElf.Blockchains.BasicBaseChain
{
    [DependsOn(
        typeof(DPoSConsensusAElfModule),
        typeof(KernelAElfModule),
        typeof(OSAElfModule),
        typeof(AbpAspNetCoreModule),
        typeof(CSharpRuntimeAElfModule),
        typeof(GrpcNetworkModule),

        //TODO: should move to OSAElfModule
        typeof(ChainControllerRpcModule),
        typeof(WalletRpcModule),
        typeof(NetRpcAElfModule),
        typeof(RuntimeSetupAElfModule),
        typeof(GrpcCrossChainAElfModule),

        //web api module
        typeof(WebWebAppAElfModule)
    )]
    public class BasicBaseChainAElfModule : AElfModule<BasicBaseChainAElfModule>
    {
        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            var config = context.Services.GetConfiguration();
                                    
            Configure<TokenInitialOptions>(option =>
            {
                var nodeType = config.GetValue<NodeType>("NodeType", NodeType.MainNet);
                switch (nodeType)
                {
                    case NodeType.MainNet:
                        option.Symbol = "ELF";
                        option.Name = "elf token";
                        option.TotalSupply = 10_0000_0000;
                        option.Decimals = 2;
                        option.IsBurnable = true;
                        option.DividendPoolRatio = 0.2;
                        option.LockForElection = 10_0000;
                        break;
                    case NodeType.TestNet:
                        option.Symbol = "ELFTEST";
                        option.Name = "elf test token";
                        option.TotalSupply = 10_0000_0000;
                        option.Decimals = 2;
                        option.IsBurnable = true;
                        option.DividendPoolRatio = 0.2;
                        option.LockForElection = 10_0000;
                        break;
                    case NodeType.CustomNet:
                        option.Symbol = config.GetValue<string>("TokenInitial:Symbol");
                        option.Name = config.GetValue<string>("TokenInitial:Name");
                        option.TotalSupply = config.GetValue<int>("TokenInitial:TotalSupply");
                        option.Decimals = config.GetValue<int>("TokenInitial:Decimals");
                        option.IsBurnable = config.GetValue<bool>("TokenInitial:IsBurnable");
                        option.DividendPoolRatio = config.GetValue<double>("TokenInitial:DividendPoolRatio");
                        option.LockForElection = config.GetValue<long>("TokenInitial:LockForElection");
                        break;
                }
            });
        }
    }
    
    public enum NodeType
    {
        MainNet,
        TestNet,
        CustomNet
    }
}