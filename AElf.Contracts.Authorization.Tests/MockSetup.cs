using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AElf.ChainController;
using AElf.Kernel;
using AElf.Kernel.Managers;
using AElf.Kernel.Storages;
using AElf.SmartContract;
using Google.Protobuf;
using ServiceStack;
using    AElf.Common;
using AElf.Database;
using AElf.Runtime.CSharp;
using AElf.SmartContract.Metadata;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AElf.Contracts.Authorization.Tests
{
    public class MockSetup
    {
        // IncrementId is used to differentiate txn
        // which is identified by From/To/IncrementId
        private static int _incrementId = 0;
        public ulong NewIncrementId()
        {
            var n = Interlocked.Increment(ref _incrementId);
            return (ulong)n;
        }

        public Hash ChainId { get; } = Hash.LoadByteArray(ChainHelpers.GetRandomChainId());
        
        public IStateStore StateStore { get; private set; }
        public ISmartContractManager SmartContractManager;
        public ISmartContractService SmartContractService;
        public IChainService ChainService;
        
        private IFunctionMetadataService _functionMetadataService;
        private IChainCreationService _chainCreationService;
        private ISmartContractRunnerFactory _smartContractRunnerFactory;
        public ILogger<MockSetup> Logger {get;set;}
        private IDataStore _dataStore;

        public MockSetup()
        {
            Logger = NullLogger<MockSetup>.Instance;
            Initialize();
        }

        private void Initialize()
        {
            NewStorage();
            var transactionManager = new TransactionManager(_dataStore);
            var transactionTraceManager = new TransactionTraceManager(_dataStore);
            _functionMetadataService = new FunctionMetadataService(_dataStore);
            var chainManager = new ChainManager(_dataStore);
            ChainService = new ChainService(chainManager, new BlockManager(_dataStore),
                transactionManager, transactionTraceManager, _dataStore, StateStore);
            _smartContractRunnerFactory = new SmartContractRunnerFactory();
            var runner = new SmartContractRunner("../../../../AElf.Runtime.CSharp.Tests.TestContract/bin/Debug/netstandard2.0/");
            _smartContractRunnerFactory.AddRunner(0, runner);
            _chainCreationService = new ChainCreationService(ChainService,
                new SmartContractService(new SmartContractManager(_dataStore), _smartContractRunnerFactory,
                    StateStore, _functionMetadataService));
            SmartContractManager = new SmartContractManager(_dataStore);
            Task.Factory.StartNew(async () =>
            {
                await Init();
            }).Unwrap().Wait();
            SmartContractService = new SmartContractService(SmartContractManager, _smartContractRunnerFactory, StateStore, _functionMetadataService);
            ChainService = new ChainService(new ChainManager(_dataStore), new BlockManager(_dataStore), new TransactionManager(_dataStore), new TransactionTraceManager(_dataStore), _dataStore, StateStore);
        }

        private void NewStorage()
        {
            var db = new InMemoryDatabase();
            StateStore = new StateStore(db);
            _dataStore = new DataStore(db);
        }
        
        public byte[] AuthorizationCode
        {
            get
            {
                byte[] code = null;
                using (FileStream file = File.OpenRead(Path.GetFullPath("../../../../AElf.Contracts.Authorization/bin/Debug/netstandard2.0/AElf.Contracts.Authorization.dll")))
                {
                    code = file.ReadFully();
                }
                return code;
            }
        }
        
        public byte[] SCZeroContractCode
        {
            get
            {
                byte[] code = null;
                using (FileStream file = File.OpenRead(Path.GetFullPath("../../../../AElf.Contracts.Genesis/bin/Debug/netstandard2.0/AElf.Contracts.Genesis.dll")))
                {
                    code = file.ReadFully();
                }
                return code;
            }
        }
        
        private async Task Init()
        {
            var reg1 = new SmartContractRegistration
            {
                Category = 0,
                ContractBytes = ByteString.CopyFrom(AuthorizationCode),
                ContractHash = Hash.FromRawBytes(AuthorizationCode),
                SerialNumber = GlobalConfig.AuthorizationContract
            };
            var reg0 = new SmartContractRegistration
            {
                Category = 0,
                ContractBytes = ByteString.CopyFrom(SCZeroContractCode),
                ContractHash = Hash.FromRawBytes(SCZeroContractCode),
                SerialNumber = GlobalConfig.GenesisBasicContract
            };

            var chain1 =
                await _chainCreationService.CreateNewChainAsync(ChainId,
                    new List<SmartContractRegistration> {reg0, reg1});
        }
        
        public async Task<IExecutive> GetExecutiveAsync(Address address)
        {
            var executive = await SmartContractService.GetExecutiveAsync(address, ChainId);
            return executive;
        }
    }
}