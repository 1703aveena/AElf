﻿using System.Threading.Tasks;
using AElf.Kernel.Storages;

namespace AElf.Kernel.Managers
{
    public class SmartContractManager : ISmartContractManager
    {
        private readonly ISmartContractStore _smartContractStore;

        public async Task<SmartContractRegistration> GetAsync(Hash contractHash)
        {
            return await _smartContractStore.GetAsync(contractHash);
        }

        public async Task InsertAsync(SmartContractRegistration reg)
        {
            await _smartContractStore.InsertAsync(reg.ContractHash, reg);
        }
    }
}