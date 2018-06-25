﻿using System.Threading;
using System.Threading.Tasks;
using AElf.Kernel;
using AElf.Kernel.Concurrency.Execution;
using AElf.Kernel.Concurrency.Metadata;
using AElf.Kernel.KernelAccount;
using AElf.Kernel.Managers;
using AElf.Kernel.Services;
using AElf.Kernel.Storages;
using AElf.Runtime.CSharp;
using Google.Protobuf;
using Path = AElf.Kernel.Path;

namespace AElf.Contracts.DPoS.Tests
{
    public class DPoSMockSetup
    {
        // IncrementId is used to differentiate txn
        // which is identified by From/To/IncrementId
        private static int _incrementId = 0;
        public ulong NewIncrementId()
        {
            var n = Interlocked.Increment(ref _incrementId);
            return (ulong)n;
        }

        public Hash ChainId1 { get; } = Hash.Generate();
        public ISmartContractManager SmartContractManager;
        public ISmartContractService SmartContractService;

        public IAccountDataProvider DataProvider1;

        public ServicePack ServicePack;

        private IWorldStateManager _worldStateManager;
        private IChainCreationService _chainCreationService;
        private IBlockManager _blockManager;
        private IFunctionMetadataService _functionMetadataService;
        
        private ISmartContractRunnerFactory _smartContractRunnerFactory;

        public DPoSMockSetup(IWorldStateManager worldStateManager, IChainCreationService chainCreationService, 
            IBlockManager blockManager, ISmartContractStore smartContractStore, IChainContextService chainContextService,
            IFunctionMetadataService functionMetadataService, ISmartContractRunnerFactory smartContractRunnerFactory)
        {
            _worldStateManager = worldStateManager;
            _chainCreationService = chainCreationService;
            _blockManager = blockManager;
            _functionMetadataService = functionMetadataService;
            _smartContractRunnerFactory = smartContractRunnerFactory;
            SmartContractManager = new SmartContractManager(smartContractStore);
            Task.Factory.StartNew(async () =>
            {
                await Init();
            }).Unwrap().Wait();
            SmartContractService = new SmartContractService(SmartContractManager, _smartContractRunnerFactory, _worldStateManager, _functionMetadataService);

            ServicePack = new ServicePack()
            {
                ChainContextService = chainContextService,
                SmartContractService = SmartContractService,
                ResourceDetectionService = null,
                WorldStateManager = _worldStateManager
            };
        }

        private async Task Init()
        {
            DataProvider1 = (await _worldStateManager.OfChain(ChainId1)).GetAccountDataProvider(Path.CalculatePointerForAccountZero(ChainId1));
        }

        public async Task DeployContractAsync(byte[] code, Hash address)
        {
            var reg = new SmartContractRegistration
            {
                Category = 0,
                ContractBytes = ByteString.CopyFrom(code),
                ContractHash = new Hash(code)
            };

            await SmartContractService.DeployContractAsync(ChainId1, address, reg);
        }

        public async Task<IExecutive> GetExecutiveAsync(Hash address)
        {
            var executive = await SmartContractService.GetExecutiveAsync(address, ChainId1);
            return executive;
        }

    }
}
