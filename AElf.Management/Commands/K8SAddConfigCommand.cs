﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf.Common.Enums;
using AElf.Configuration;
using AElf.Configuration.Config.GRPC;
using AElf.Configuration.Config.Management;
using AElf.Management.Helper;
using AElf.Management.Models;
using k8s;
using k8s.Models;
using Microsoft.AspNetCore.JsonPatch;
using Uri = AElf.Configuration.Config.GRPC.Uri;

namespace AElf.Management.Commands
{
    public class K8SAddConfigCommand : IDeployCommand
    {
        public async Task Action(DeployArg arg)
        {
            var body = new V1ConfigMap
            {
                ApiVersion = V1ConfigMap.KubeApiVersion,
                Kind = V1ConfigMap.KubeKind,
                Metadata = new V1ObjectMeta
                {
                    Name = GlobalSetting.CommonConfigName,
                    NamespaceProperty = arg.SideChainId
                },
                Data = new Dictionary<string, string>
                {
                    {"actor.json", GetActorConfigJson(arg)},
                    {"database.json", GetDatabaseConfigJson(arg)},
                    {"miners.json", GetMinersConfigJson(arg)},
                    {"parallel.json", GetParallelConfigJson(arg)},
                    {"network.json", GetNetworkConfigJson(arg)},
                    {"grpc-local.json", GetGrpcConfigJson(arg)},
                    {"grpc-remote.json", GetGrpcRemoteConfigJson(arg)},
                    {"api-key.json", GetApiKeyConfig(arg)}
                }
            };

            await K8SRequestHelper.GetClient().CreateNamespacedConfigMapAsync(body, arg.SideChainId);

            if (!arg.IsDeployMainChain)
            {
                var config = await K8SRequestHelper.GetClient().ReadNamespacedConfigMapAsync(GlobalSetting.CommonConfigName, arg.MainChainId);

                var grpcRemoteConfig = JsonSerializer.Instance.Deserialize<GrpcRemoteConfig>(config.Data["grpc-remote.json"]);
                grpcRemoteConfig.ChildChains.Add(arg.SideChainId, new Uri {Port = GlobalSetting.GrpcPort, Address = arg.LauncherArg.ClusterIp});
                config.Data["grpc-remote.json"] = JsonSerializer.Instance.Serialize(grpcRemoteConfig);

                var patch = new JsonPatchDocument<V1ConfigMap>();
                patch.Replace(e => e.Data, config.Data);

                await K8SRequestHelper.GetClient().PatchNamespacedConfigMapAsync(new V1Patch(patch), GlobalSetting.CommonConfigName, arg.MainChainId);
            }
        }

        private string GetActorConfigJson(DeployArg arg)
        {
            var config = new ActorConfig
            {
                IsCluster = arg.LighthouseArg.IsCluster,
                HostName = "127.0.0.1",
                Port = 0,
                ActorCount = arg.WorkArg.ActorCount,
                ConcurrencyLevel = arg.WorkArg.ConcurrencyLevel,
                Seeds = new List<SeedNode> {new SeedNode {HostName = "set-lighthouse-0.service-lighthouse", Port = 4053}},
                SingleHoconFile = "single.hocon",
                MasterHoconFile = "master.hocon",
                WorkerHoconFile = "worker.hocon",
                LighthouseHoconFile = "lighthouse.hocon",
                MonitorHoconFile = "monitor.hocon"
            };

            var result = JsonSerializer.Instance.Serialize(config);

            return result;
        }

        private string GetDatabaseConfigJson(DeployArg arg)
        {
            return null;
        }

        private string GetMinersConfigJson(DeployArg arg)
        {
            var config = new MinersConfig();
            //Todo
            if (!arg.Miners.Contains(arg.ChainAccount))
            {
                arg.Miners.Add(arg.ChainAccount);
            }

            var i = 1;
            config.Producers = new Dictionary<string, Dictionary<string, string>>();
            foreach (var miner in arg.Miners)
            {
                config.Producers.Add(i.ToString(), new Dictionary<string, string> {{"address", miner}});
                i++;
            }

            var result = JsonSerializer.Instance.Serialize(config);

            return result;
        }

        private string GetParallelConfigJson(DeployArg arg)
        {
            var config = new ParallelConfig
            {
                IsParallelEnable = false
            };

            var result = JsonSerializer.Instance.Serialize(config);

            return result;
        }

        private string GetNetworkConfigJson(DeployArg arg)
        {
//            var config = new NetworkConfig();
//            config.Bootnodes = new List<string>();
//
//            if (arg.LauncherArg.Bootnodes != null && arg.LauncherArg.Bootnodes.Any())
//            {
//                config.Bootnodes = arg.LauncherArg.Bootnodes;
//            }
//
//            var result = JsonSerializer.Instance.Serialize(config);
//
//            return result;
            // Todo rewrite this
            return null;
        }

        private string GetGrpcConfigJson(DeployArg arg)
        {
            var config = new GrpcLocalConfig
            {
                LocalServerIP = "0.0.0.0", //arg.LauncherArg.ClusterIp,
                LocalSideChainServerPort = GlobalSetting.GrpcPort,
                ClientToParentChain = true,
                ClientToSideChain = true,
                WaitingIntervalInMillisecond = 10,
                SideChainServer = true
            };

            var result = JsonSerializer.Instance.Serialize(config);

            return result;
        }

        private string GetGrpcRemoteConfigJson(DeployArg arg)
        {
            var config = new GrpcRemoteConfig()
            {
                ParentChain = new Dictionary<string, Uri>(),
                ChildChains = new Dictionary<string, Uri>()
            };

            if (!arg.IsDeployMainChain)
            {
                var service = K8SRequestHelper.GetClient().ReadNamespacedService(GlobalSetting.LauncherServiceName, arg.MainChainId);
                config.ParentChain.Add(arg.MainChainId, new Uri {Port = GlobalSetting.GrpcPort, Address = service.Spec.ClusterIP});
            }

            var result = JsonSerializer.Instance.Serialize(config);

            return result;
        }

        private string GetApiKeyConfig(DeployArg arg)
        {
            arg.ApiKey = Guid.NewGuid().ToString("N");
            var config = new ApiKeyConfig
            {
                ChainKeys = new Dictionary<string, string> {{arg.SideChainId, arg.ApiKey}}
            };

            var result = JsonSerializer.Instance.Serialize(config);

            return result;
        }
    }
}