﻿using System.Threading.Tasks;
using AElf.Kernel.Storages;
using AElf.Kernel.Types;

namespace AElf.Kernel.Managers
{
    public class SmartContractManager : ISmartContractManager
    {
        private readonly IDataStore _dataStore;

        public SmartContractManager(IDataStore dataStore)
        {
            _dataStore = dataStore;
        }

        public async Task<SmartContractRegistration> GetAsync(Hash contractHash)
        {
            return await _dataStore.GetAsync<SmartContractRegistration>(contractHash);
        }

        public async Task InsertAsync(Hash address, SmartContractRegistration reg)
        {
            await _dataStore.InsertAsync(address, reg);
        }
    }
}