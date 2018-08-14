﻿using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf.SmartContract;
using AElf.ChainController;
using AElf.Kernel.Managers;
using AElf.Kernel.Storages;
using AElf.Kernel.TxMemPool;
using NLog;
using Xunit;
using Xunit.Frameworks.Autofac;

namespace AElf.Kernel.Tests
{
    [UseAutofacTestFramework]
    public class DataProviderTest
    {
        private readonly IWorldStateStore _worldStateStore;
        private readonly IChangesStore _changesStore;
        private readonly IDataStore _dataStore;
        private readonly BlockTest _blockTest;
        private readonly ILogger _logger;
        private readonly ITransactionManager _transactionManager;
        private readonly IBlockManagerBasic _blockManager;
        private readonly IPointerManager _pointerManager;

        public DataProviderTest(IWorldStateStore worldStateStore,
            IChangesStore changesStore, IDataStore dataStore,
            BlockTest blockTest, ILogger logger, ITransactionManager transactionManager, IBlockManagerBasic blockManager, IPointerManager pointerManager)
        {
            _worldStateStore = worldStateStore;
            _changesStore = changesStore;
            _dataStore = dataStore;
            _blockTest = blockTest;
            _logger = logger;
            _transactionManager = transactionManager;
            _blockManager = blockManager;
            _pointerManager = pointerManager;
        }

        [Fact]
        public async Task SetTest()
        {
            const int count = 5;
            var setList = CreateSet(count).ToList();
            var keys = GenerateKeys(setList).ToList();

            var chain = await _blockTest.CreateChain();

            var address = Hash.Generate();

            var worldStateDictator =
                new WorldStateDictator(_worldStateStore, _changesStore, _dataStore,_logger, _transactionManager, 
                        _blockManager, _pointerManager)
                    .SetChainId(chain.Id);
            worldStateDictator.BlockProducerAccountAddress = Hash.Generate();

            await worldStateDictator.SetWorldStateAsync(chain.GenesisBlockHash);
            
            var accountDataProvider = await worldStateDictator.GetAccountDataProvider(address);
            var dataProvider = accountDataProvider.GetDataProvider();

            for (var i = 0; i < count; i++)
            {
                await dataProvider.SetAsync(keys[i], setList[i]);
            }

            for (var i = 0; i < count; i++)
            {
                var getData = await dataProvider.GetAsync(keys[i]);
                Assert.True(getData.SequenceEqual(setList[i]));
            }

            for (var i = 0; i < count - 1; i++)
            {
                var getData = await dataProvider.GetAsync(keys[i]);
                Assert.False(getData.SequenceEqual(setList[i + 1]));
            }
        }
        
        private IEnumerable<byte[]> CreateSet(int count)
        {
            var list = new List<byte[]>(count);
            for (var i = 0; i < count; i++)
            {
                list.Add(Hash.Generate().GetHashBytes());
            }

            return list;
        }

        private IEnumerable<Hash> GenerateKeys(IEnumerable<byte[]> set)
        {
           return set.Select(data => new Hash(data.CalculateHash())).ToList();
        }
    }
}