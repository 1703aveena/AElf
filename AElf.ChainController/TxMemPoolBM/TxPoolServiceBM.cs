﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AElf.ChainController.TxMemPool;
using AElf.Kernel;
using AElf.Kernel.Managers;
using NLog;

namespace AElf.ChainController.TxMemPoolBM
{
    public class TxPoolServiceBM : ITxPoolService
    {
        private readonly ILogger _logger;
        private readonly ITxValidator _txValidator;
        private int Least { get; set; }
        private int Limit { get; set; }

        public TxPoolServiceBM(ILogger logger, ITxValidator txValidator, TxHub txHub)
        {
            _logger = logger;
            _txValidator = txValidator;
            _txHub = txHub;
        }

        public void Start()
        {
        }

        public Task Stop()
        {
            return Task.CompletedTask;
        }

        private TxHub _txHub;

        private readonly ConcurrentDictionary<Hash, Transaction> _systemTxs =
            new ConcurrentDictionary<Hash, Transaction>();

        /// <inheritdoc/>
        public async Task<TxValidation.TxInsertionAndBroadcastingError> AddTxAsync(Transaction tx)
        {
            var txv = _txHub.GetTxHolderView(tx.GetHash());
            if (txv != null)
            {
                return TxValidation.TxInsertionAndBroadcastingError.KnownTx;
            }

            var res = await AddTransaction(tx);
            return res;
        }

        /// <summary>
        /// enqueue tx in pool
        /// </summary>
        /// <param name="tx"></param>
        /// <returns></returns>
        private async Task<TxValidation.TxInsertionAndBroadcastingError> AddTransaction(Transaction tx)
        {
            var txid = tx.GetHash();
            var nonSys = tx.Type == TransactionType.ContractTransaction;
            if (nonSys)
            {
                try
                {
                    _txHub.AddNewTransaction(tx);
                }
                catch (Exception)
                {
                    return TxValidation.TxInsertionAndBroadcastingError.AlreadyInserted;
                }
                _txHub.ValidatingTx(txid);
            }

            var res = _txValidator.ValidateTx(tx);
            if (res != TxValidation.TxInsertionAndBroadcastingError.Valid)
            {
                if (nonSys)
                {
                    _txHub.InvalidatedTx(txid);
                }

                return res;
            }

            res = await _txValidator.ValidateReferenceBlockAsync(tx);
            if (res != TxValidation.TxInsertionAndBroadcastingError.Valid)
            {
                if (nonSys)
                {
                    _txHub.InvalidatedTx(txid);
                }

                return res;
            }

            if (nonSys)
            {
                _txHub.ValidatedTx(txid);
            }
            
            if (!nonSys)
            {
                return await Task.FromResult(AddSystemTransaction(tx));
            }
            return TxValidation.TxInsertionAndBroadcastingError.Success;
//            return await Task.FromResult(AddContractTransaction(tx));
        }

        /// <summary>
        /// enqueue system tx
        /// </summary>
        /// <param name="tx"></param>
        /// <returns></returns>
        private TxValidation.TxInsertionAndBroadcastingError AddSystemTransaction(Transaction tx)
        {
            if (tx.Type == TransactionType.ContractTransaction)
                return TxValidation.TxInsertionAndBroadcastingError.Failed;
            if (_systemTxs.ContainsKey(tx.GetHash()))
                return TxValidation.TxInsertionAndBroadcastingError.AlreadyInserted;

            return _systemTxs.TryAdd(tx.GetHash(), tx)
                ? TxValidation.TxInsertionAndBroadcastingError.Success
                : TxValidation.TxInsertionAndBroadcastingError.Failed;
        }

        /// <summary>
        /// enqueue contract tx
        /// </summary>
        /// <param name="tx"></param>
        /// <returns></returns>
        private TxValidation.TxInsertionAndBroadcastingError AddContractTransaction(Transaction tx)
        {
            if (tx.Type != TransactionType.ContractTransaction)
                return TxValidation.TxInsertionAndBroadcastingError.Failed;

            try
            {
                _txHub.AddNewTransaction(tx);
            }
            catch (Exception)
            {
                return TxValidation.TxInsertionAndBroadcastingError.AlreadyInserted;
            }
//            
//            if (_contractTxs.ContainsKey(tx.GetHash()))
//                return TxValidation.TxInsertionAndBroadcastingError.AlreadyInserted;
//
//            if (_contractTxs.TryAdd(tx.GetHash(), tx))
//            {
                return TxValidation.TxInsertionAndBroadcastingError.Success;
//            }

            return TxValidation.TxInsertionAndBroadcastingError.Failed;
        }

        /// <inheritdoc/>
        public async Task Revert(List<Transaction> txsOut)
        {
            foreach (var tx in txsOut)
            {
                if (tx.Type == TransactionType.ContractTransaction)
                {
//                    AddContractTransaction(tx);
                    _txHub.RevertExecutingTx(tx.GetHash());   
                }
                else
                {
                    AddSystemTransaction(tx);
                }
                tx.Unclaim();
            }

            await Task.CompletedTask;
        }

        /// <inheritdoc/>
        public bool TryGetTx(Hash txHash, out Transaction tx)
        {
            tx = _txHub.GetTxHolderView(txHash)?.Transaction;
            if (tx == null)
            {
                _systemTxs.TryGetValue(txHash, out tx);
            }
            return tx != null;
        }

        public List<Hash> GetMissingTransactions(IBlock block)
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

        /// <inheritdoc/>
        public void RemoveAsync(Hash txHash)
        {
            if (_systemTxs.TryRemove(txHash, out _))
                return;
            _txHub.ExecutedTx(txHash);
//            _contractTxs.TryRemove(txHash, out _);
        }

        /// <inheritdoc/>
        public async Task<List<Transaction>> GetReadyTxsAsync(Hash blockProducerAddress, double intervals = 150)
        {
            // TODO: Improve performance
            var txs = _systemTxs.Values.ToList();

            RemoveDirtySystemTxs(txs, blockProducerAddress);
            _logger.Debug($"Got {txs.Count} DPoS tx");
            var count = _txHub.ValidatedCount;
            if (count < Least)
            {
                _logger.Debug($"Regular txs {Least} required, but we only have {count}");
                return txs;
            }

            txs.AddRange(_txHub.GetTxsForExecution(Limit));

            _logger.Debug($"Got {txs.Count} total tx");
            return txs;
        }
        
        private void RemoveDirtySystemTxs(List<Transaction> readyTxs, Hash blockProducerAddress)
        {
            const string inValueTxName = "PublishInValue";
            var toRemove = new List<Transaction>();
            foreach (var transaction in readyTxs)
            {
                if (transaction.From == blockProducerAddress)
                    continue;
                
                if (transaction.Type == TransactionType.CrossChainBlockInfoTransaction || 
                    transaction.Type == TransactionType.DposTransaction && transaction.MethodName != inValueTxName)
                {
                    toRemove.Add(transaction);
                }
            }

            // No one will publish in value if I won't do this in current block.
            if (!readyTxs.Any(tx => tx.MethodName == inValueTxName && tx.From == blockProducerAddress))
            {
                toRemove.AddRange(readyTxs.FindAll(tx => tx.MethodName == inValueTxName));
            }
            else
            {
                // One BP can only publish in value once in one block.
                toRemove.AddRange(readyTxs.FindAll(tx => tx.MethodName == inValueTxName).GroupBy(tx => tx.From)
                    .Where(g => g.Count() > 1).SelectMany(g => g));
            }
            
            foreach (var transaction in toRemove)
            {
                readyTxs.Remove(transaction);
            }
        }
        
        public List<Transaction> GetSystemTxs()
        {
            return _systemTxs.Values.ToList();
        }

        /// <inheritdoc/>
        public Task UpdateAccountContext(HashSet<Hash> addrs)
        {
            // todo remove
            return Task.CompletedTask;
        }

        public void SetBlockVolume(int minimal, int maximal)
        {
            Least = minimal;
            Limit = maximal;
        }

        /// <inheritdoc/>
        public Task RemoveTxWithWorstFeeAsync()
        {
            throw new NotImplementedException();
        }

        public Task<ulong> GetPoolSize()
        {
            return Task.FromResult((ulong) _txHub.ValidatedCount);
        }
    }
}