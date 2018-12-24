﻿using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AElf.Kernel;
using AElf.ChainController;
using AElf.SmartContract;
using AElf.Execution;
using Google.Protobuf;
using AElf.Kernel.Tests;
using AElf.Common;
using AElf.Execution.Execution;
using AElf.Kernel.Manager.Interfaces;
using AElf.Kernel.Manager.Managers;

namespace AElf.Sdk.CSharp.Tests
{
    public class MockSetup
    {
        // IncrementId is used to differentiate txn
        // which is identified by From/To/IncrementId
        private static int _incrementId = 0;

        public ulong NewIncrementId()
        {
            var n = Interlocked.Increment(ref _incrementId);
            return (ulong) n;
        }

        public Hash ChainId1 { get; } = Hash.LoadByteArray(new byte[] {0x01, 0x02, 0x03});
        public ISmartContractManager SmartContractManager;
        public ISmartContractService SmartContractService;
        private IFunctionMetadataService _functionMetadataService;

        public IChainContextService ChainContextService;

        public IStateManager StateManager;
        public DataProvider DataProvider1;

        public ServicePack ServicePack;

        private IChainCreationService _chainCreationService;

        private ISmartContractRunnerFactory _smartContractRunnerFactory;

        public MockSetup(IStateManager stateManager, IChainCreationService chainCreationService,
            IChainContextService chainContextService, IFunctionMetadataService functionMetadataService,
            ISmartContractRunnerFactory smartContractRunnerFactory, ISmartContractManager smartContractManager)
        {
            StateManager = stateManager;
            _chainCreationService = chainCreationService;
            ChainContextService = chainContextService;
            _functionMetadataService = functionMetadataService;
            _smartContractRunnerFactory = smartContractRunnerFactory;
            SmartContractManager = smartContractManager;
            Task.Factory.StartNew(async () => { await Init(); }).Unwrap().Wait();
            SmartContractService = new SmartContractService(SmartContractManager, _smartContractRunnerFactory,
                StateManager, _functionMetadataService);

            ServicePack = new ServicePack()
            {
                ChainContextService = chainContextService,
                SmartContractService = SmartContractService,
                ResourceDetectionService = null,
                StateManager = StateManager
            };
        }

        public byte[] SmartContractZeroCode
        {
            get { return ContractCodes.TestContractZeroCode; }
        }

        private async Task Init()
        {
            var reg = new SmartContractRegistration
            {
                Category = 0,
                ContractBytes = ByteString.CopyFrom(SmartContractZeroCode),
                ContractHash = Hash.Zero
            };
            var chain1 =
                await _chainCreationService.CreateNewChainAsync(ChainId1, new List<SmartContractRegistration> {reg});

            DataProvider1 = DataProvider.GetRootDataProvider(
                chain1.Id,
                Address.Generate() // todo warning adr contract adress
            );
            DataProvider1.StateManager = StateManager;
        }

        public async Task DeployContractAsync(byte[] code, Address address)
        {
            var reg = new SmartContractRegistration
            {
                Category = 1,
                ContractBytes = ByteString.CopyFrom(code),
                ContractHash = Hash.FromRawBytes(code)
            };

            await SmartContractService.DeployContractAsync(ChainId1, address, reg, false);
        }

        public async Task<IExecutive> GetExecutiveAsync(Address address)
        {
            var executive = await SmartContractService.GetExecutiveAsync(address, ChainId1);
            return executive;
        }
    }
}