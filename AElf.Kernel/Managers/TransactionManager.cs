﻿using System.Threading.Tasks;
using AElf.Common;
using AElf.Kernel.Storages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AElf.Kernel.Managers
{
    public class TransactionManager: ITransactionManager
    {
        private readonly ITransactionStore _transactionStore;
        public ILogger<TransactionManager> Logger {get;set;}

        public TransactionManager(ITransactionStore transactionStore)
        {
            _transactionStore = transactionStore;
            Logger = NullLogger<TransactionManager>.Instance;
        }

        public async Task<Hash> AddTransactionAsync(Transaction tx)
        {
            var txHash = tx.GetHash();
            await _transactionStore.SetAsync(GetStringKey(txHash), tx);
            return txHash;
        }

        public async Task<Transaction> GetTransaction(Hash txId)
        {
            return await _transactionStore.GetAsync<Transaction>(GetStringKey(txId));
        }

        public async Task RemoveTransaction(Hash txId)
        {
            await _transactionStore.RemoveAsync(GetStringKey(txId));
        }
        
        private string GetStringKey(Hash txId)
        {
            return txId.ToHex();
        }
    }
}