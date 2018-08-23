﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AElf.ChainController.EventMessages;
using AElf.Common.Attributes;
using AElf.Common.Synchronisation;
using AElf.Kernel;
using AElf.SmartContract;
using Easy.MessageHub;
using NLog;
using ReaderWriterLock = AElf.Common.Synchronisation.ReaderWriterLock;

namespace AElf.ChainController.TxMemPool
{
    [LoggerName("Txpool")]
    public class TxPoolService : ChainController.TxMemPool.ITxPoolService
    {
        private readonly IContractTxPool _contractTxPool;
        private readonly IDPoSTxPool _dpoSTxPool;
        private readonly IAccountContextService _accountContextService;
        private readonly ILogger _logger;

        public TxPoolService(IContractTxPool contractTxPool, IAccountContextService accountContextService, 
            ILogger logger, IDPoSTxPool dpoSTxPool)
        {
            _contractTxPool = contractTxPool;
            _accountContextService = accountContextService;
            _logger = logger;
            _dpoSTxPool = dpoSTxPool;
        }

        /// <summary>
        /// Signals to a CancellationToken that it should be canceled
        /// </summary>
        private CancellationTokenSource Cts { get; set; }

        private TxPoolSchedulerLock ContractTxLock { get; } = new TxPoolSchedulerLock();
        private TxPoolSchedulerLock DPoSTxLock { get; } = new TxPoolSchedulerLock();


        private readonly ConcurrentDictionary<Hash, ITransaction> _contractTxs = new ConcurrentDictionary<Hash, ITransaction>();
        private readonly ConcurrentDictionary<Hash, ITransaction> _dPoSTxs = new ConcurrentDictionary<Hash, ITransaction>();
        private readonly ConcurrentBag<Hash> _bpAddrs = new ConcurrentBag<Hash>();

        /// <inheritdoc/>
        public async Task<TxValidation.TxInsertionAndBroadcastingError> AddTxAsync(ITransaction tx)
        {
            if (Cts.IsCancellationRequested) return TxValidation.TxInsertionAndBroadcastingError.PoolClosed;
            
            var res = await AddTransaction(tx);
            if (res == TxValidation.TxInsertionAndBroadcastingError.Success)
            {
                MessageHub.Instance.Publish(new TransactionAddedToPool(tx));
            }

            return res;
        }

        /// <summary>
        /// enqueue tx in pool
        /// </summary>
        /// <param name="tx"></param>
        /// <returns></returns>
        private async Task<TxValidation.TxInsertionAndBroadcastingError> AddTransaction(ITransaction tx)
        {
            if (tx.Type == TransactionType.DposTransaction)
            {
                await TrySetNonce(tx.From, TransactionType.DposTransaction);
                return await DPoSTxLock.WriteLock(() => AddDPoSTransaction(tx));
            }

            await TrySetNonce(tx.From, TransactionType.ContractTransaction);
            return await ContractTxLock.WriteLock(() => AddContractTransaction(tx));
        }

        
        /// <summary>
        /// enqueue dpos tx
        /// </summary>
        /// <param name="tx"></param>
        /// <returns></returns>
        private TxValidation.TxInsertionAndBroadcastingError AddDPoSTransaction(ITransaction tx)
        {
            if (tx.Type != TransactionType.DposTransaction) return TxValidation.TxInsertionAndBroadcastingError.Failed;
            if (_dPoSTxs.ContainsKey(tx.GetHash()))
                return TxValidation.TxInsertionAndBroadcastingError.AlreadyInserted;
            
            var res = _dpoSTxPool.EnQueueTx(tx);
            if (res == TxValidation.TxInsertionAndBroadcastingError.Success)
            {
                // add tx
                _dPoSTxs.GetOrAdd(tx.GetHash(), tx);
                _bpAddrs.Add(tx.From);
            }
            return res;
        }
        
        /// <summary>
        /// enqueue contract tx
        /// </summary>
        /// <param name="tx"></param>
        /// <returns></returns>
        private TxValidation.TxInsertionAndBroadcastingError AddContractTransaction(ITransaction tx)
        {
            if (tx.Type != TransactionType.ContractTransaction)
                return TxValidation.TxInsertionAndBroadcastingError.Failed;
            if (_contractTxs.ContainsKey(tx.GetHash()))
                return TxValidation.TxInsertionAndBroadcastingError.AlreadyInserted;
            var res = _contractTxPool.EnQueueTx(tx);
            if (res == TxValidation.TxInsertionAndBroadcastingError.Success)
            {
                // add tx
                _contractTxs.GetOrAdd(tx.GetHash(), tx);
            }
            return res;
        }
        
        /// <summary>
        /// set nonce for address in tx pool
        /// </summary>
        /// <param name="addr"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        private async Task TrySetNonce(Hash addr, TransactionType type)
        {
            IPool pool;
            if(type == TransactionType.ContractTransaction)
                pool =  _contractTxPool;
            else
                pool = _dpoSTxPool;
            // tx from account state
            if (!pool.GetNonce(addr).HasValue)
            {
                var incrementId = (await _accountContextService.GetAccountDataContext(addr, pool.ChainId))
                    .IncrementId;
                pool.TrySetNonce(addr, incrementId);
            }
        }


        /// <inheritdoc/>
        public void RemoveAsync(Hash txHash)
        {
            if (_dPoSTxs.TryRemove(txHash, out _))
                return;
            _contractTxs.TryRemove(txHash, out _);
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
        public async Task<List<ITransaction>> GetReadyTxsAsync()
        {
            // get dpos transanction
            var dpos = await DPoSTxLock.WriteLock(() =>
            {
                var readyTxs = _dpoSTxPool.ReadyTxs();
                foreach (var tx in readyTxs)
                {
                    _dPoSTxs.TryRemove(tx.GetHash(), out _);
                }
                _logger.Log(LogLevel.Debug, $"Got {readyTxs.Count} DPoS tx");
                return readyTxs;
            });
            
            List<ITransaction> contractTxs = null;
            bool available = false;
            ulong count = 0;
            var tokenSource = new CancellationTokenSource();
            var token = tokenSource.Token;
            tokenSource.CancelAfter(TimeSpan.FromMilliseconds(150));
            var t = ContractTxLock.WriteLock(() =>
            {
                if (token.IsCancellationRequested)
                    return;
                _logger.Log(LogLevel.Debug, "Got lock");
                // TODO: remove this limit
                available = true;
                var execCount = _contractTxPool.GetExecutableSize();
                count = execCount;
                if (execCount < _contractTxPool.Least)
                {
                    return;
                }

                if (token.IsCancellationRequested)
                    return;

                _logger.Log(LogLevel.Debug, $"ready tx count == {count}");
                contractTxs = _contractTxPool.ReadyTxs();
            }, token).Wait(TimeSpan.FromMilliseconds(150));
            
            if (contractTxs != null)
            {
                _logger.Log(LogLevel.Debug, $"got contract tx count == {contractTxs.Count}");
                // get txs successfully
                dpos.AddRange(contractTxs);
            
                foreach (var tx in contractTxs)
                {
                    _contractTxs.TryRemove(tx.GetHash(), out _);
                }
                _logger.Log(LogLevel.Debug, $"Got {contractTxs.Count} Contract tx");
            }
            else if(!available)
            {
                // timeout to get lock
                _logger.Log(LogLevel.Debug, "TIMEOUT! - Unable to get Contract transactions");
            }
            else if(count < _contractTxPool.Least)
            {
                // not enough tx count
                _logger.Log(LogLevel.Debug,
                    $"{count} Contract tx(s) in pool are ready:  less than {_contractTxPool.Least}");
            }
            else
            {
                _logger.Log(LogLevel.Error, "FAILED to get all transactions，some would be lost!");
            }
                
            
            return dpos;
        }

        /// <inheritdoc/>
        public Task<bool> GetReadyTxsAsync(Hash addr, ulong start, ulong ids)
        {
            return _bpAddrs.Contains(addr)
                ? DPoSTxLock.WriteLock(() => { return _dpoSTxPool.ReadyTxs(addr, start, ids); })
                : ContractTxLock.WriteLock(() => { return _contractTxPool.ReadyTxs(addr, start, ids); });
        }


        /// <inheritdoc/>
        public async Task<ulong> GetPoolSize()
        {
            /*lock (this)
            {
                return Task.FromResult(_contractTxPool.Size);
            }*/

            return await ContractTxLock.ReadLock(() => _contractTxPool.Size) +
                   await DPoSTxLock.ReadLock(() => _dpoSTxPool.Size);
        }

        /// <inheritdoc/>
        public bool TryGetTx(Hash txHash, out ITransaction tx)
        {
            return _contractTxs.TryGetValue(txHash, out tx) || _dPoSTxs.TryGetValue(txHash, out tx);
        }
        
        public List<Hash> GetMissingTransactions(IBlock block)
        {
            try
            {
                var res = new List<Hash>();
                var txs = block.Body.Transactions;
                foreach (var id in txs)
                {
                    if (!TryGetTx(id, out _))
                    {
                        res.Add(id);
                    }
                }

                return res;
            }
            catch (Exception e)
            {
                _logger?.Trace("Error while getting missing transactions");
                return null;
            }
        }

        /// <inheritdoc/>
        public async Task<ulong> GetWaitingSizeAsync()
        {
            return await ContractTxLock.ReadLock(() => _contractTxPool.GetWaitingSize()) +
                   await DPoSTxLock.ReadLock(() => _dpoSTxPool.GetWaitingSize());
        }

        /// <inheritdoc/>
        public async Task<ulong> GetExecutableSizeAsync()
        {
            return await ContractTxLock.ReadLock(() => _contractTxPool.GetExecutableSize()) +
                   await DPoSTxLock.ReadLock(() => _dpoSTxPool.GetExecutableSize());
        }        


        /// <inheritdoc/>
        public async Task UpdateAccountContext(HashSet<Hash> addrs)
        {
            _logger?.Log(LogLevel.Debug, "Updating Account Context..");
            foreach (var addr in addrs)
            {
                IPool pool;
                if (!_bpAddrs.Contains(addr))
                    pool = _contractTxPool;
                else
                {
                    pool = _dpoSTxPool;
                }
                var id = pool.GetNonce(addr);
                if(!id.HasValue)
                    continue;
                
                // update account context
                await _accountContextService.SetAccountContext(new AccountDataContext
                {
                    IncrementId = id.Value,
                    Address = addr,
                    ChainId = pool.ChainId
                });
            }
            _logger?.Log(LogLevel.Debug, "End Updating Account Context..");

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
            /*await ContractTxLock.WriteLock(() =>
            {
                // TODO: release resources
                Cts.Cancel();
                Cts.Dispose();
                //EnqueueEvent.Dispose();
                //DemoteEvent.Dispose();
            });*/
            return ContractTxLock.WriteLock(() =>
            {
                Cts.Cancel();
                Cts.Dispose();
            });
        }


        /// <inheritdoc/>
        public ulong GetIncrementId(Hash addr, bool isDPoS = false)
        {
            ILock @lock;
            IPool pool;
            if (!isDPoS)
            {
                pool = _contractTxPool;
            }
            else
            {
                pool = _dpoSTxPool;
            }
            return pool.GetPendingIncrementId(addr);
        }


        /// <inheritdoc/>
        public async Task RollBack(List<ITransaction> txsOut)
        {
            try
            {
                var nonces = txsOut.Select(async p => await TrySetNonce(p.From, p.Type));
                await Task.WhenAll(nonces);

                var tmap = txsOut.Aggregate(new Dictionary<Hash, HashSet<ITransaction>>(),  (current, p) =>
                {
                    if (!current.TryGetValue(p.From, out _))
                    {
                        current[p.From] = new HashSet<ITransaction>();
                    }

                    current[p.From].Add(p);
                
                    return current;
                });
                _logger?.Log(LogLevel.Debug, "Start txpool rollback");

                foreach (var kv in tmap)
                {
                    
                    ILock @lock;
                    IPool pool;
                    if (!_bpAddrs.Contains(kv.Key))
                    {
                        @lock = ContractTxLock;
                        pool = _contractTxPool;
                    }
                    else
                    {
                        @lock = DPoSTxLock;
                        pool = _dpoSTxPool;
                    }
                    var nonce = pool.GetNonce(kv.Key);
                    var min = kv.Value.Min(t => t.IncrementId);
                    var n = nonce ?? (await _accountContextService.GetAccountDataContext(kv.Key, pool.ChainId))
                            .IncrementId;
                    
                    // cannot be rollbacked
                    if(min >= n)
                        continue;
                    
                    await _accountContextService.SetAccountContext(new AccountDataContext
                    {
                        IncrementId = min,
                        Address = kv.Key,
                        ChainId = pool.ChainId
                    });
                    
                    await @lock.WriteLock(() =>
                    {
                        pool.Withdraw(kv.Key, min);
                        foreach (var tx in kv.Value)
                        {
                            if (@lock == DPoSTxLock)
                            {
                                AddDPoSTransaction(tx);
                            }
                            else
                            {
                                AddContractTransaction(tx);
                            }
                        }
                    });
                }
                _logger?.Log(LogLevel.Debug, "Start txpool rollback");

            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public void SetBlockVolume(ulong minimal, ulong maximal)
        {
            _contractTxPool.SetBlockVolume(minimal, maximal);
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