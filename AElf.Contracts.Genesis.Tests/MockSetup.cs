﻿using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;
using AElf.Kernel;
using AElf.ChainController;
using AElf.SmartContract;
using AElf.Kernel.KernelAccount;
using AElf.Kernel.Managers;
using AElf.Execution;
using Google.Protobuf;
using AElf.Common;
using AElf.Execution.Execution;
using Volo.Abp.DependencyInjection;

namespace AElf.Contracts.Genesis.Tests
{
    public class MockSetup : ITransientDependency
    {
        // IncrementId is used to differentiate txn
        // which is identified by From/To/IncrementId
        private static int _incrementId;
        public ulong NewIncrementId()
        {
            var n = Interlocked.Increment(ref _incrementId);
            return (ulong)n;
        }

        public IStateManager StateManager { get; }
        public Hash ChainId1 { get; } = Hash.LoadByteArray(new byte[] { 0x01, 0x02, 0x03 });
        public ISmartContractManager SmartContractManager;
        public ISmartContractService SmartContractService;
        private IFunctionMetadataService _functionMetadataService;

        public IChainContextService ChainContextService;

        public IAccountDataProvider DataProvider1;

        public ServicePack ServicePack;

        private IChainCreationService _chainCreationService;

        private ISmartContractRunnerContainer _smartContractRunnerContainer;

        public MockSetup(IStateManager stateManager, IChainCreationService chainCreationService,
            IChainContextService chainContextService,
            IFunctionMetadataService functionMetadataService, ISmartContractRunnerContainer smartContractRunnerContainer
            , ISmartContractManager smartContractManager)
        {
            StateManager = stateManager;
            _chainCreationService = chainCreationService;
            ChainContextService = chainContextService;
            _functionMetadataService = functionMetadataService;
            _smartContractRunnerContainer = smartContractRunnerContainer;
            SmartContractManager = smartContractManager;
            Task.Factory.StartNew(async () => { await Init(); }).Unwrap().Wait();
            SmartContractService = new SmartContractService(SmartContractManager, _smartContractRunnerContainer,
                StateManager, _functionMetadataService);

            ServicePack = new ServicePack()
            {
                ChainContextService = chainContextService,
                SmartContractService = SmartContractService,
                ResourceDetectionService = null,
                StateManager = stateManager
            };
         }

        private byte[] SmartContractZeroCode
        {
            get
            {
                byte[] code = File.ReadAllBytes(Path.GetFullPath(
                    "../../../../AElf.Contracts.Genesis/bin/Debug/netstandard2.0/AElf.Contracts.Genesis.dll"));
                return code;
            }
        }
        
        private async Task Init()
        {
            var reg = new SmartContractRegistration
            {
                Category = 0,
                ContractBytes = ByteString.CopyFrom(SmartContractZeroCode),
                ContractHash = Hash.Zero
            };
            var chain1 = await _chainCreationService.CreateNewChainAsync(ChainId1, new List<SmartContractRegistration>{reg});
        }
        
        public async Task<IExecutive> GetExecutiveAsync(Address address)
        {
            var executive = await SmartContractService.GetExecutiveAsync(address, ChainId1);
            return executive;
        }
    }
}
