﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AElf.Common.Attributes;
using AElf.Kernel.Managers;
using AElf.Kernel.Services;
using Akka.Pattern;
using NLog;
using ReaderWriterLock = AElf.Common.Synchronisation.ReaderWriterLock;

namespace AElf.Kernel.TxMemPool
{
    [LoggerName("Txpool")]
    public class TxPoolService : ITxPoolService
    {
        private readonly ITxPool _txPool;
        private readonly IAccountContextService _accountContextService;
        private readonly ITransactionManager _transactionManager;
        private readonly ITransactionResultManager _transactionResultManager;
        private readonly ILogger _logger;

        public TxPoolService(ITxPool txPool, IAccountContextService accountContextService,
            ITransactionManager transactionManager, ITransactionResultManager transactionResultManager, ILogger logger)
        {
            _txPool = txPool;
            _accountContextService = accountContextService;
            _transactionManager = transactionManager;
            _transactionResultManager = transactionResultManager;
            _logger = logger;
        }

        /// <summary>
        /// signal event for enqueue txs
        /// </summary>
        private AutoResetEvent EnqueueEvent { get; set; }

        /// <summary>
        /// signal event for demote executed txs
        /// </summary>
        private AutoResetEvent DemoteEvent { get; set; }

        /// <summary>
        /// Signals to a CancellationToken that it should be canceled
        /// </summary>
        private CancellationTokenSource Cts { get; set; }

        private TxPoolSchedulerLock Lock { get; } = new TxPoolSchedulerLock();

        private readonly ConcurrentDictionary<Hash, ITransaction> _txs = new ConcurrentDictionary<Hash, ITransaction>();

        private readonly HashSet<Hash> _addrCache = new HashSet<Hash>();

        /// <inheritdoc/>
        public async Task<TxValidation.TxInsertionAndBroadcastingError> AddTxAsync(ITransaction tx)
        {
            if (Cts.IsCancellationRequested) return TxValidation.TxInsertionAndBroadcastingError.PoolClosed;

            if (_txs.ContainsKey(tx.GetHash()))
                return TxValidation.TxInsertionAndBroadcastingError.AlreadyInserted;
            await TrySetNonce(tx.From);
            
            return AddTransaction(tx);
        }

        private TxValidation.TxInsertionAndBroadcastingError AddTransaction(ITransaction tx)
        {
            lock (this)
            {
                var res = _txPool.EnQueueTx(tx);
                if (res == TxValidation.TxInsertionAndBroadcastingError.Success)
                {
                    // add tx
                    _txs.GetOrAdd(tx.GetHash(), tx);
                }
                return res;
            }
        }


        /// <summary>
        /// set nonce for address in tx pool
        /// </summary>
        /// <param name="addr"></param>
        /// <returns></returns>
        private async Task TrySetNonce(Hash addr)
        {
            // tx from account state
            if (!_txPool.GetNonce(addr).HasValue)
            {
                var incrementId = (await _accountContextService.GetAccountDataContext(addr, _txPool.ChainId))
                    .IncrementId;
                _txPool.TrySetNonce(addr, incrementId);
            }
            _addrCache.Add(addr);
        }


        /// <inheritdoc/>
        public void RemoveAsync(Hash txHash)
        {
            lock (this)
            {
                if(_txs.TryGetValue(txHash, out var tx))
                {
                    _txs.TryRemove(tx.GetHash(), out tx);
                }
            }
            
        }

        /// <inheritdoc/>
        public Task RemoveTxWithWorstFeeAsync()
        {
            throw new NotImplementedException();
        }


        /// <summary>
        /// persist txs with storage
        /// </summary>
        /// <param name="txs"></param>
        /// <returns></returns>
        private void PersistTxs(IEnumerable<ITransaction> txs)
        {
            foreach (var t in txs)
            {
                // TODO: persist tx
                //await _transactionManager.AddTransactionAsync(tx);
            }
        }

        /// <inheritdoc/>
        public Task<List<ITransaction>> GetReadyTxsAsync(ulong limit)
        {
            //return Lock.ReadLock(() =>
            lock (this)
            {
                //_txPool.Enqueueable = false;

                var readyTxs = _txPool.ReadyTxs(limit);
                foreach (var tx in readyTxs)
                {
                    _txs.TryRemove(tx.GetHash(), out var t);
                }

                return Task.FromResult(readyTxs);
            }
        }

        /// <inheritdoc/>
        public Task<bool> GetReadyTxsAsync(Hash addr, ulong start, ulong ids)
        {
            lock (this)
            {
                return Task.FromResult(_txPool.ReadyTxs(addr, start, ids));
            }
        }

        /// <inheritdoc/>
        public Task<bool> PromoteAsync()
        {
            //return Lock.WriteLock(() =>
            lock (this)
            {
                _txPool.Promote();
                return Task.FromResult(true);
            }
        }


        /// <inheritdoc/>
        public Task PromoteAsync(List<Hash> addresses)
        {
            //return Lock.WriteLock(() =>
            lock (this)
            {
                _txPool.Promote(addresses);
                return Task.FromResult(true);
            }
        }

        /// <inheritdoc/>
        public Task<ulong> GetPoolSize()
        {
            lock (this)
            {
                return Task.FromResult(_txPool.Size);
            }

            //return Lock.ReadLock(() => _txPool.Size);
        }

        /// <inheritdoc/>
        public bool TryGetTx(Hash txHash, out ITransaction tx)
        {
            return _txs.TryGetValue(txHash, out tx);
        }

        /// <inheritdoc/>
        public Task ClearAsync()
        {
            /*return Lock.WriteLock(()=>
            {
                _txPool.ClearAll();
            });*/
            lock (this)
            {
                _txPool.ClearAll();
                return Task.CompletedTask;
            }
        }

        /// <inheritdoc/>
        public Task SavePoolAsync()
        {
            throw new System.NotImplementedException(); 
        }

        /// <inheritdoc/>
        public Task<ulong> GetWaitingSizeAsync()
        {
            //return Lock.ReadLock(() => _txPool.GetWaitingSize());
            lock (this)
            {
                return Task.FromResult(_txPool.GetWaitingSize());
            }
        }

        /// <inheritdoc/>
        public Task<ulong> GetExecutableSizeAsync()
        {
            //return Lock.ReadLock(() => _txPool.GetExecutableSize());
            lock (this)
            {
                return Task.FromResult(_txPool.GetExecutableSize());
            }
        }        


        /// <inheritdoc/>
        public async Task ResetAndUpdate(HashSet<Hash> addrs)
        {
            foreach (var addr in addrs)
            {
                var id = _txPool.GetNonce(addr);
                if(!id.HasValue)
                    continue;
                
                // update account context
                await _accountContextService.SetAccountContext(new AccountDataContext
                {
                    IncrementId = id.Value,
                    Address = addr,
                    ChainId = _txPool.ChainId
                });
            }
        }

        /// <inheritdoc/>
        public void Start()
        {
            // TODO: more initialization
            Cts = new CancellationTokenSource();
        }


        /// <inheritdoc/>
        public Task Stop()
        {
            /*await Lock.WriteLock(() =>
            {
                // TODO: release resources
                Cts.Cancel();
                Cts.Dispose();
                //EnqueueEvent.Dispose();
                //DemoteEvent.Dispose();
            });*/
            lock (this)
            {
                Cts.Cancel();
                Cts.Dispose();
                return Task.CompletedTask;
            }
        }

        /// <inheritdoc/>
        public ulong GetIncrementId(Hash addr)
        {
            lock (this)
            {
                return _txPool.GetPendingIncrementId(addr);
            }
        }


        /// <inheritdoc/>
        public async Task RollBack(List<ITransaction> txsOut)
        {
            var files = txsOut.Select(async p => await TrySetNonce(p.From));
            await Task.WhenAll(files);
            
            var tmap = txsOut.Aggregate(new Dictionary<Hash, HashSet<ITransaction>>(),  (current, p) =>
            {
                if (!current.TryGetValue(p.From, out var txs))
                {
                    current[p.From] = new HashSet<ITransaction>();
                }

                current[p.From].Add(p);
                
                return current;
            });

            foreach (var kv in tmap)
            {
                var nonce = _txPool.GetNonce(kv.Key);
                var min = kv.Value.Min(t => t.IncrementId);
                if(min >= nonce.Value)
                    continue;
                
                _txPool.Withdraw(kv.Key, min);
                foreach (var tx in kv.Value)
                {
                    if (_txs.ContainsKey(tx.GetHash()))
                        continue;
                    AddTransaction(tx);
                }
                
                await _accountContextService.SetAccountContext(new AccountDataContext
                {
                    IncrementId = min,
                    Address = kv.Key,
                    ChainId = _txPool.ChainId
                });
            }
            
        }
    }

    /// <inheritdoc />
    /// <summary>
    /// A lock for managing asynchronous access to memory pool.
    /// </summary>
    public class TxPoolSchedulerLock : ReaderWriterLock
    {
    }
}