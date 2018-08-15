﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AElf.ChainController;
using AElf.Kernel.Managers;
using AElf.Kernel.Storages;
using AsyncEventAggregator;
using Xunit;
using Xunit.Frameworks.Autofac;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;

namespace AElf.Kernel.Tests
{
    [UseAutofacTestFramework]
    public class ChainTest
    {
        private readonly IChainCreationService _chainCreationService;
        //private readonly ISmartContractZero _smartContractZero;
        private readonly IChainService _chainService;
        private readonly IWorldStateStore _worldStateStore;
        private readonly IChangesStore _changesStore;
        private readonly IDataStore _dataStore;
        

        public ChainTest(IChainCreationService chainCreationService,
            IChainService chainService, IWorldStateStore worldStateStore, 
            IChangesStore changesStore, IDataStore dataStore)
        {
            //_smartContractZero = smartContractZero;
            _chainCreationService = chainCreationService;
            _chainService = chainService;
            _worldStateStore = worldStateStore;
            _changesStore = changesStore;
            _dataStore = dataStore;
            this.Subscribe<IBlock>(async (t) => { await Task.CompletedTask;});
        }

        public byte[] SmartContractZeroCode
        {
            get
            {
                return ContractCodes.TestContractZeroCode;
            }
        }

        [Fact]
        public async Task<IChain> CreateChainTest()
        {
            var reg = new SmartContractRegistration
            {
                Category = 0,
                ContractBytes = ByteString.CopyFrom(SmartContractZeroCode),
                ContractHash = Hash.Zero
            };

            var chainId = Hash.Generate();
            var chain = await _chainCreationService.CreateNewChainAsync(chainId, new List<SmartContractRegistration>{reg});

            // add chain to storage
            
            //var address = Hash.Generate();
            //var worldStateManager = await new WorldStateManager(_worldStateStore, 
            //    _changesStore, _dataStore).SetChainId(chainId);
            //var accountDataProvider = worldStateManager.GetAccountDataProvider(address);
            
            //await _smartContractZero.InitializeAsync(accountDataProvider);
            var blockchain = _chainService.GetBlockChain(chainId);
            var getNextHeight = new Func<Task<ulong>>(async () =>
            {
                var curHash = await blockchain.GetCurrentBlockHashAsync();
                var indx = ((BlockHeader) await blockchain.GetHeaderByHashAsync(curHash)).Index;
                return indx + 1;
            });
            Assert.Equal(await getNextHeight(), (ulong)1);
            return chain;
        }

//        public async Task ChainStoreTest(Hash chainId)
//        {
//            await _chainManager.AddChainAsync(chainId, Hash.Generate());
//            Assert.NotNull(_chainManager.GetChainAsync(chainId).Result);
//        }
        

        [Fact]
        public async Task AppendBlockTest()
        {
            var reg = new SmartContractRegistration
            {
                Category = 0,
                ContractBytes = ByteString.CopyFrom(SmartContractZeroCode),
                ContractHash = Hash.Zero
            };

            var chainId = Hash.Generate();
            var chain = await _chainCreationService.CreateNewChainAsync(chainId, new List<SmartContractRegistration>{reg});

            // add chain to storage
            
            //var address = Hash.Generate();
            //var worldStateManager = await new WorldStateManager(_worldStateStore, 
            //    _changesStore, _dataStore).SetChainId(chainId);
            //var accountDataProvider = worldStateManager.GetAccountDataProvider(address);
            
            //await _smartContractZero.InitializeAsync(accountDataProvider);
            var blockchain = _chainService.GetBlockChain(chainId);
            var getNextHeight = new Func<Task<ulong>>(async () =>
            {
                var curHash = await blockchain.GetCurrentBlockHashAsync();
                var indx = ((BlockHeader) await blockchain.GetHeaderByHashAsync(curHash)).Index;
                return indx + 1;
            });
            Assert.Equal(await getNextHeight(), (ulong)1);

            var block = CreateBlock(chain.GenesisBlockHash, chain.Id, 1);
            await blockchain.AddBlocksAsync(new List<IBlock>(){ block });
//            await _chainManager.AppendBlockToChainAsync(block);
            Assert.Equal(await getNextHeight(), (ulong)2);
            Assert.Equal(await blockchain.GetCurrentBlockHashAsync(), block.GetHash());
            Assert.Equal(block.Header.Index, (ulong)1);
        }
        
        private Block CreateBlock(Hash preBlockHash, Hash chainId, ulong index)
        {
            Interlocked.CompareExchange(ref preBlockHash, Hash.Zero, null);
            
            var block = new Block(Hash.Generate());
            block.AddTransaction(Hash.Generate());
            block.AddTransaction(Hash.Generate());
            block.AddTransaction(Hash.Generate());
            block.AddTransaction(Hash.Generate());
            block.FillTxsMerkleTreeRootInHeader();
            block.Header.PreviousBlockHash = preBlockHash;
            block.Header.ChainId = chainId;
            block.Header.Time = Timestamp.FromDateTime(DateTime.UtcNow);
            block.Header.Index = index;
            block.Header.MerkleTreeRootOfWorldState = Hash.Default;

            return block;
        }


        
    }
}