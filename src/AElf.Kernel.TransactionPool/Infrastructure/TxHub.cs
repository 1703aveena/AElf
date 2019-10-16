using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf.Kernel.Blockchain.Application;
using AElf.Kernel.Blockchain.Domain;
using AElf.Kernel.Blockchain.Events;
using AElf.Types;
using AElf.Kernel.SmartContractExecution.Application;
using AElf.Kernel.TransactionPool.Application;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Local;

namespace AElf.Kernel.TransactionPool.Infrastructure
{
    public class TxHub : ITxHub, ISingletonDependency
    {
        public ILogger<TxHub> Logger { get; set; }
        private readonly TransactionOptions _transactionOptions;

        private readonly ITransactionManager _transactionManager;
        private readonly IBlockchainService _blockchainService;
        private readonly ITransactionValidationService _transactionValidationService;

        private ConcurrentQueue<TransactionReceipt> _helpQueue =
            new ConcurrentQueue<TransactionReceipt>();

        private readonly ConcurrentQueue<TransactionReceipt> _validatedTransactions =
            new ConcurrentQueue<TransactionReceipt>();

        private ConcurrentDictionary<long, ConcurrentDictionary<Hash, TransactionReceipt>> _invalidatedByBlock =
            new ConcurrentDictionary<long, ConcurrentDictionary<Hash, TransactionReceipt>>();

        private ConcurrentDictionary<long, ConcurrentDictionary<Hash, TransactionReceipt>> _expiredByExpiryBlock =
            new ConcurrentDictionary<long, ConcurrentDictionary<Hash, TransactionReceipt>>();

        private ConcurrentDictionary<long, ConcurrentDictionary<Hash, TransactionReceipt>> _futureByBlock =
            new ConcurrentDictionary<long, ConcurrentDictionary<Hash, TransactionReceipt>>();

        private long _bestChainHeight = Constants.GenesisBlockHeight - 1;
        private Hash _bestChainHash = Hash.Empty;

        public ILocalEventBus LocalEventBus { get; set; }

        public TxHub(ITransactionManager transactionManager, IBlockchainService blockchainService,
            IOptionsSnapshot<TransactionOptions> transactionOptions,
            ITransactionValidationService transactionValidationService)
        {
            Logger = NullLogger<TxHub>.Instance;
            _transactionManager = transactionManager;
            _blockchainService = blockchainService;
            _transactionValidationService = transactionValidationService;
            LocalEventBus = NullLocalEventBus.Instance;
            _transactionOptions = transactionOptions.Value;
        }

        public async Task<ExecutableTransactionSet> GetExecutableTransactionSetAsync(int transactionCount = 0)
        {
            var output = new ExecutableTransactionSet
            {
                PreviousBlockHash = _bestChainHash,
                PreviousBlockHeight = _bestChainHeight
            };

            if (transactionCount == -1)
            {
                return output;
            }

            var chain = await _blockchainService.GetChainAsync();
            if (chain.BestChainHash != _bestChainHash)
            {
                Logger.LogWarning(
                    $"Attempting to retrieve executable transactions while best chain records don't match.");
                return output;
            }

            output.Transactions.AddRange(_validatedTransactions
                .Where((x, i) => transactionCount <= 0 || i < transactionCount).Select(x => x.Transaction));

            return output;
        }

        public Task<TransactionReceipt> GetTransactionReceiptAsync(Hash transactionId)
        {
            var receipt =  _validatedTransactions.FirstOrDefault(x => x.TransactionId == transactionId);
            return Task.FromResult(receipt);
        }

        #region Private Methods

        #region Private Static Methods

        private static void AddToCollection(
            ConcurrentDictionary<long, ConcurrentDictionary<Hash, TransactionReceipt>> collection,
            TransactionReceipt receipt)
        {
            if (!collection.TryGetValue(receipt.Transaction.RefBlockNumber, out var receipts))
            {
                receipts = new ConcurrentDictionary<Hash, TransactionReceipt>();
                collection.TryAdd(receipt.Transaction.RefBlockNumber, receipts);
            }

            receipts.TryAdd(receipt.TransactionId, receipt);
        }

        private static void CheckPrefixForOne(TransactionReceipt receipt, ByteString prefix, long bestChainHeight)
        {
            if (receipt.Transaction.GetExpiryBlockNumber() <= bestChainHeight)
            {
                receipt.RefBlockStatus = RefBlockStatus.RefBlockExpired;
                return;
            }

            if (prefix == null)
            {
                receipt.RefBlockStatus = RefBlockStatus.FutureRefBlock;
                return;
            }

            if (receipt.Transaction.RefBlockPrefix == prefix)
            {
                receipt.RefBlockStatus = RefBlockStatus.RefBlockValid;
                return;
            }

            receipt.RefBlockStatus = RefBlockStatus.RefBlockInvalid;
        }

        #endregion

        private ByteString GetPrefixByHash(Hash hash)
        {
            return hash == null ? null : ByteString.CopyFrom(hash.ToByteArray().Take(4).ToArray());
        }

        private async Task<ByteString> GetPrefixByHeightAsync(long height, Hash bestChainHash)
        {
            var chain = await _blockchainService.GetChainAsync();
            var hash = await _blockchainService.GetBlockHashByHeightAsync(chain, height, bestChainHash);
            return GetPrefixByHash(hash);
        }

        private async Task<Dictionary<long, ByteString>> GetPrefixesByHeightAsync(long firstHeight, Hash bestChainHash,
            long bestChainHeight)
        {
            var blockIndexes =
                await _blockchainService.GetBlockIndexesAsync(firstHeight, bestChainHash, bestChainHeight);

            return blockIndexes.ToDictionary(blockIndex => blockIndex.Height,
                blockIndex => GetPrefixByHash(blockIndex.Hash));
        }

        private void ResetCurrentCollections()
        {
            _expiredByExpiryBlock = new ConcurrentDictionary<long, ConcurrentDictionary<Hash, TransactionReceipt>>();
            _invalidatedByBlock = new ConcurrentDictionary<long, ConcurrentDictionary<Hash, TransactionReceipt>>();
            _futureByBlock = new ConcurrentDictionary<long, ConcurrentDictionary<Hash, TransactionReceipt>>();
        }

        private void AddToRespectiveCurrentCollection(TransactionReceipt receipt)
        {
            switch (receipt.RefBlockStatus)
            {
                case RefBlockStatus.RefBlockExpired:
                    AddToCollection(_expiredByExpiryBlock, receipt);
                    break;
                case RefBlockStatus.FutureRefBlock:
                    AddToCollection(_futureByBlock, receipt);
                    break;
                case RefBlockStatus.RefBlockInvalid:
                    AddToCollection(_invalidatedByBlock, receipt);
                    break;
                case RefBlockStatus.RefBlockValid:
                    if (!_validatedTransactions.Contains(receipt))
                    {
                        _validatedTransactions.Enqueue(receipt);
                    }
                    break;
            }
        }
        
        private void UpdateValidatedTransactions()
        {
            _helpQueue = new ConcurrentQueue<TransactionReceipt>();
            while (_validatedTransactions.TryDequeue(out var receipt))
            {
                if (receipt.RefBlockStatus != RefBlockStatus.RefBlockValid)
                {
                    continue;
                }
                
                _helpQueue.Enqueue(receipt);
            }

            while (_helpQueue.TryDequeue(out var receipt))
            {
                _validatedTransactions.Enqueue(receipt);
            }
        }

        private void CleanTransactions(ConcurrentDictionary<long, ConcurrentDictionary<Hash, TransactionReceipt>>
            collection, long blockHeight)
        {
            var keys = collection.Where(kv => kv.Key <= blockHeight).Select(x => x.Key);
            foreach (var key in keys)
            {
                collection.TryRemove(key, out _);
            }
        }

        private void CleanTransactions(IEnumerable<Hash> transactionIds)
        {
            while (_validatedTransactions.TryDequeue(out var receipt))
            {
                if (transactionIds != null && transactionIds.Contains(receipt.TransactionId))
                {
                    continue;
                }

                _helpQueue.Enqueue(receipt);
            }

            while (_helpQueue.TryDequeue(out var receipt))
            {
                _validatedTransactions.Enqueue(receipt);
            }
        }

        #endregion

        #region Event Handler Methods

        public async Task HandleTransactionsReceivedAsync(TransactionsReceivedEvent eventData)
        {
            foreach (var transaction in eventData.Transactions)
            {
                var receipt = new TransactionReceipt
                {
                    TransactionId = transaction.GetHash(),
                    Transaction = transaction
                };
                if (_validatedTransactions.Any(receiptEx => receiptEx.TransactionId == receipt.TransactionId)
                || _expiredByExpiryBlock.Values.Any(x => x.Values.Any(y => y.TransactionId == receipt.TransactionId)))
                {
                    //Logger.LogWarning($"Transaction already exists in TxStore");
                    continue;
                }

                var transactionCount = _validatedTransactions.Count + _expiredByExpiryBlock.Count +
                                        _futureByBlock.Count + _invalidatedByBlock.Count;
                if (transactionCount > _transactionOptions.PoolLimit)
                {
                    //Logger.LogWarning($"TxStore is full, ignore tx {receipt.TransactionId}");
                    break;
                }

                // Skip this transaction if it is already in local database.
                var txn = await _transactionManager.GetTransactionAsync(receipt.TransactionId);
                if (txn != null)
                {
                    continue;
                }

                var validationResult = await _transactionValidationService.ValidateTransactionAsync(transaction);
                if (!validationResult)
                {
                    continue;
                }

                await _transactionManager.AddTransactionAsync(transaction);

                if (_bestChainHash == Hash.Empty)
                {
                    continue;
                }

                var prefix = await GetPrefixByHeightAsync(receipt.Transaction.RefBlockNumber, _bestChainHash);
                CheckPrefixForOne(receipt, prefix, _bestChainHeight);
                AddToRespectiveCurrentCollection(receipt);
                if (receipt.RefBlockStatus == RefBlockStatus.RefBlockValid)
                {
                    await LocalEventBus.PublishAsync(new TransactionAcceptedEvent()
                    {
                        Transaction = transaction
                    });
                }
            }
        }

        public async Task HandleBlockAcceptedAsync(BlockAcceptedEvent eventData)
        {
            var block = await _blockchainService.GetBlockByHashAsync(eventData.BlockHeader.GetHash());
            CleanTransactions(block.Body.TransactionIds.ToList());
        }

        public async Task HandleBestChainFoundAsync(BestChainFoundEventData eventData)
        {
            Logger.LogDebug(
                $"Handle best chain found: BlockHeight: {eventData.BlockHeight}, BlockHash: {eventData.BlockHash}");

            var allTxsCount = _validatedTransactions.Count + _expiredByExpiryBlock.Count +
                                   _futureByBlock.Count + _invalidatedByBlock.Count;
            var minimumHeight = allTxsCount == 0
                ? 0
                : _validatedTransactions.Min(x => x.Transaction.RefBlockNumber);
            var prefixes = await GetPrefixesByHeightAsync(minimumHeight, eventData.BlockHash, eventData.BlockHeight);

            foreach (var receipt in _validatedTransactions)
            {
                prefixes.TryGetValue(receipt.Transaction.RefBlockNumber, out var prefix);
                CheckPrefixForOne(receipt, prefix, _bestChainHeight);
            }

            UpdateValidatedTransactions();
            _bestChainHash = eventData.BlockHash;
            _bestChainHeight = eventData.BlockHeight;

            Logger.LogDebug(
                $"Finish handle best chain found: BlockHeight: {eventData.BlockHeight}, BlockHash: {eventData.BlockHash}");
        }

        public async Task HandleNewIrreversibleBlockFoundAsync(NewIrreversibleBlockFoundEvent eventData)
        {
            CleanTransactions(_expiredByExpiryBlock, eventData.BlockHeight);
            CleanTransactions(_invalidatedByBlock, eventData.BlockHeight);

            await Task.CompletedTask;
        }

        public async Task HandleUnexecutableTransactionsFoundAsync(UnexecutableTransactionsFoundEvent eventData)
        {
            CleanTransactions(eventData.Transactions);

            await Task.CompletedTask;
        }

        #endregion

        public Task<int> GetAllTransactionCountAsync()
        {
            var allTxsCount = _validatedTransactions.Count + _expiredByExpiryBlock.Count +
                              _futureByBlock.Count + _invalidatedByBlock.Count;
            return Task.FromResult(allTxsCount);
        }

        public Task<int> GetValidatedTransactionCountAsync()
        {
            var validatedTxCount = _validatedTransactions.Count;
            return Task.FromResult(validatedTxCount);
        }
    }
}