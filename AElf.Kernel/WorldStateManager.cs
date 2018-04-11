﻿using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.ComTypes;
using System.Threading.Tasks;
using AElf.Kernel.Storages;

namespace AElf.Kernel
{
    public class WorldStateManager: IWorldStateManager
    {
        private readonly IWorldStateStore _worldStateStore;
        private readonly IPointerStore _pointerStore;
        private readonly Hash _blockHash;
        private readonly IAccountContextService _accountContextService;


        public WorldStateManager(IWorldStateStore worldStateStore, Hash blockHash, 
            IAccountContextService accountContextService, IPointerStore pointerStore)
        {
            _worldStateStore = worldStateStore;
            _blockHash = blockHash;
            _accountContextService = accountContextService;
            _pointerStore = pointerStore;
        }

        public Hash GenesisBlockHash { get; set; }

        public Task<WorldState> GetWorldStateAsync(Hash chainId)
        {
            return Task.FromResult(_worldStateStore.GetWorldState(chainId, _blockHash));
        }

        public IAccountDataProvider GetAccountDataProvider(Hash chainId, Hash accountHash)
        {
            return new AccountDataProvider(accountHash, chainId, _accountContextService,
                _pointerStore, _worldStateStore);
        }
    }
}