﻿using System.Collections.Generic;
using System.Threading.Tasks;
using AElf.Common;
using AElf.Kernel.Blockchain.Events;
using AElf.Kernel.Node.Infrastructure;
using AElf.Kernel.TransactionPool.Application;

namespace AElf.Kernel.TransactionPool.Infrastructure
{
    public class ExecutableTransactionSet
    {
        public int ChainId { get; set; }
        public Hash PreviousBlockHash { get; set; }
        public ulong PreviousBlockHeight { get; set; }
        public List<Transaction> Transactions { get; set; } = new List<Transaction>();
    }

    public interface ITxHub : IChainRelatedComponent
    {
        Task<ExecutableTransactionSet> GetExecutableTransactionSetAsync();
        Task HandleTransactionsReceivedAsync(TransactionsReceivedEvent eventData);
        Task HandleBlockAcceptedAsync(BlockAcceptedEvent eventData);
        Task HandleBestChainFoundAsync(BestChainFoundEventData eventData);
        Task HandleNewIrreversibleBlockFoundAsync(NewIrreversibleBlockFoundEvent eventData);
        Task<TransactionReceipt> GetTransactionReceiptAsync(Hash transactionId);
    }
}