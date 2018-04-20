﻿using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AElf.Kernel.Storages;
using ReaderWriterLock = AElf.Kernel.Lock.ReaderWriterLock;

namespace AElf.Kernel.TxMemPool
{
    public class TxPoolService : ITxPoolService
    {
        private readonly ITxPool _txPool;
        private readonly ITransactionManager _transactionManager;

        public TxPoolService(ITxPool txPool, ITransactionManager transactionManager)
        {
            _txPool = txPool;
            _transactionManager = transactionManager;
        }

        /// <summary>
        /// signal event for multi-thread
        /// </summary>
        private AutoResetEvent Are { get; } = new AutoResetEvent(false);
        
        /// <summary>
        /// Signals to a CancellationToken that it should be canceled
        /// </summary>
        private CancellationTokenSource Cts { get; } = new CancellationTokenSource();
        
        private HashSet<Transaction> Tmp { get; } = new HashSet<Transaction>();

        private TxPoolSchedulerLock Lock { get; } = new TxPoolSchedulerLock();

        /// <inheritdoc/>
        public Task<bool> AddTransaction(Transaction tx)
        {
            return Cts.IsCancellationRequested ? Task.FromResult(false) : Lock.WriteLock(() => Tmp.Add(tx));
        }
        
        /// <inheritdoc/>
        public Task AddTxsToPool(List<Transaction> txs)
        {
            return Lock.WriteLock(() =>
            {
                foreach (var tx in txs)
                {
                    _txPool.AddTx(tx);
                }
                return Task.CompletedTask;
            });
        }

        
        /// <summary>
        /// wait new tx
        /// </summary> 
        /// <returns></returns>
        private async Task Receive()
        {
            // TODO: need interupt waiting 
            while (!Cts.IsCancellationRequested)
            {
                // wait for signal
                Are.WaitOne();

                var transactions = await Lock.WriteLock(() =>
                {
                    var txs = Tmp.Where(t => !_txPool.Contains(t.From)).ToList();
                    // clear tmp txs
                    Tmp.Clear();
                    return txs;
                });
                
                if(transactions.Count == 0)
                    continue;
                
                await AddTxsToPool(transactions);
            }
        }
        

        /// <inheritdoc/>
        public Task Remove(Hash txHash)
        {
            Lock.WriteLock(() => _txPool.DisgardTx(txHash));
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task RemoveTxWithWorstFee()
        {
            throw new System.NotImplementedException();
        }

        /// <inheritdoc/>
        public async Task RemoveTxsExecuted(Block block)
        {
            var txHashes = block.Body.Transactions;
            foreach (var hash in txHashes)
            {
                await Remove(hash);
            }

            // Sets the state of the event to signaled, allowing one or more waiting threads to proceed
            Are.Set();
        }

        /// <inheritdoc/>
        public async Task PersistTxs(IEnumerable<Hash> txHashes)
        {
            foreach (var h in txHashes)
            {
                await Persist(h);
            }
        }

        private async Task Persist(Hash txHash)
        {
            if (!await GetTransaction(txHash, out var tx))
            {
                // TODO: tx not found, log error
            }
            else
            {
                await _transactionManager.AddTransactionAsync(tx);
            }
        }
        
        /// <inheritdoc/>
        public Task<List<Transaction>> GetReadyTxs()
        {
            return Lock.ReadLock(() => _txPool.Ready);
        }

        /// <inheritdoc/>
        public Task<ulong> GetPoolSize()
        {
            return Lock.ReadLock(() => _txPool.Size);
        }

        /// <inheritdoc/>
        public Task<bool> GetTransaction(Hash txHash, out Transaction tx)
        {
            tx = Lock.ReadLock(() => _txPool.GetTransaction(txHash)).Result;
            return Task.FromResult(tx != null);
        }

        /// <inheritdoc/>
        public Task Clear()
        {
            return Lock.WriteLock(()=>
            {
                _txPool.ClearAll();
                return Task.CompletedTask;
            });
        }

        /// <inheritdoc/>
        public Task SavePool()
        {
            throw new System.NotImplementedException();
        }

        /// <inheritdoc/>
        public void Start()
        {
            // TODO: more initialization
            Tmp.Clear();
            Task.Factory.StartNew(async () => await Receive());
        }

        /// <inheritdoc/>
        public void Stop()
        {
            // TODO: release resources
            Cts.Cancel();
            Are.Dispose();
        }
    }
    
    /// <summary>
    /// A lock for managing asynchronous access to memory pool.
    /// </summary>
    public class TxPoolSchedulerLock : ReaderWriterLock
    {
    }
}