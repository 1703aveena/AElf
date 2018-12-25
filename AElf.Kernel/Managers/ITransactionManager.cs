﻿using System.Threading.Tasks;
using AElf.Common;

namespace AElf.Kernel.Managers
{
    public interface ITransactionManager
    {
        Task<Hash> AddTransactionAsync(Transaction tx);
        Task<Transaction> GetTransaction(Hash txId);
        Task RemoveTransaction(Hash txId);
    }
}