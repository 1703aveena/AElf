﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AElf.ChainController;
using AElf.ChainController.EventMessages;
using AElf.ChainController.TxMemPool;
using AElf.Common.Attributes;
using AElf.Common.ByteArrayHelpers;
using AElf.Configuration;
using AElf.Cryptography.ECDSA;
using AElf.Kernel;
using AElf.Kernel.Managers;
using AElf.Miner.Rpc.Server;
using AElf.SmartContract;
using AElf.Types.CSharp;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Easy.MessageHub;
using NLog;
using NServiceKit.Common.Extensions;
using ITxPoolService = AElf.ChainController.TxMemPool.ITxPoolService;
using Status = AElf.Kernel.Status;
using AElf.Common;

// ReSharper disable once CheckNamespace
namespace AElf.Miner.Miner
{
    // ReSharper disable IdentifierTypo
    [LoggerName(nameof(Miner))]
    public class Miner : IMiner
    {
        private readonly ITxPoolService _txPoolService;
        private ECKeyPair _keyPair;
        private readonly IChainService _chainService;
        private readonly IStateDictator _stateDictator;
        private readonly IExecutingService _executingService;
        private readonly ITransactionManager _transactionManager;
        private readonly ITransactionResultManager _transactionResultManager;
        private int _timeoutMilliseconds;
        private readonly ILogger _logger;
        private IBlockChain _blockChain;
        private readonly ClientManager _clientManager;
        private readonly IBinaryMerkleTreeManager _binaryMerkleTreeManager;
        private readonly ServerManager _serverManager;

        private IMinerConfig Config { get; }

        public Address Coinbase => Config.CoinBase;

        public Miner(IMinerConfig config, ITxPoolService txPoolService, IChainService chainService,
            IStateDictator stateDictator, IExecutingService executingService, ITransactionManager transactionManager,
            ITransactionResultManager transactionResultManager, ILogger logger, ClientManager clientManager, 
            IBinaryMerkleTreeManager binaryMerkleTreeManager, ServerManager serverManager)
        {
            Config = config;
            _txPoolService = txPoolService;
            _chainService = chainService;
            _stateDictator = stateDictator;
            _executingService = executingService;
            _transactionManager = transactionManager;
            _transactionResultManager = transactionResultManager;
            _logger = logger;
            _clientManager = clientManager;
            _binaryMerkleTreeManager = binaryMerkleTreeManager;
            _serverManager = serverManager;
            var chainId = config.ChainId;
            _stateDictator.ChainId = chainId;
        }

        /// <summary>
        /// Mine process.
        /// </summary>
        /// <param name="currentRoundInfo"></param>
        /// <returns></returns>
        public async Task<IBlock> Mine(Round currentRoundInfo = null)
        {
            using (var cancellationTokenSource = new CancellationTokenSource())
            using (var timer = new Timer(s => cancellationTokenSource.Cancel()))
            {
                timer.Change(_timeoutMilliseconds, Timeout.Infinite);
                try
                {
                    if (cancellationTokenSource.IsCancellationRequested)
                        return null;

                    var parentChainBlockInfo = await GetParentChainBlockInfo();
                    Transaction genTx= await GenerateTransactionWithParentChainBlockInfo(parentChainBlockInfo);
                    var readyTxs = await _txPoolService.GetReadyTxsAsync(currentRoundInfo, _stateDictator.BlockProducerAccountAddress);
                    var bn = await _blockChain.GetCurrentBlockHeightAsync();
                    
                    // remove invalid CrossChainBlockInfoTransaction, only that from local can be executed)
                    /*readyTxs.RemoveAll(t =>
                        t.Type == TransactionType.CrossChainBlockInfoTransaction &&
                        !t.GetHash().Equals(genTx.GetHash()));*/
                    
                    var dposTxs = readyTxs.Where(tx => tx.Type == TransactionType.DposTransaction);
                    _logger?.Trace($"Will package {dposTxs.Count()} DPoS txs.");
                    foreach (var transaction in dposTxs)
                    {
                        _logger?.Trace($"{transaction.GetHash().Dumps()} - {transaction.MethodName} from {transaction.From.Dumps()}");
                    }
                    
                    _logger?.Log(LogLevel.Debug, "Executing Transactions..");
                    var traces = readyTxs.Count == 0
                        ? new List<TransactionTrace>()
                        : await _executingService.ExecuteAsync(readyTxs, Config.ChainId, cancellationTokenSource.Token);
                    _logger?.Log(LogLevel.Debug, "Executed Transactions.");

                    // transaction results
                    ExtractTransactionResults(readyTxs, traces, out var executed, out var rollback, out var results);

                    // generate block
                    var block = await GenerateBlockAsync(Config.ChainId, results);
                    _logger?.Log(LogLevel.Debug, $"Generated Block at height {block.Header.Index}");

                    // broadcast
                    BroadcastBlock(block);
                    
                    // append block
                    await _blockChain.AddBlocksAsync(new List<IBlock> {block});
                    // put back canceled transactions
                    // No await so that it won't affect Consensus
                    _txPoolService.Revert(rollback);
                    // insert to db
                    Update(executed, results, block, parentChainBlockInfo, genTx);
                    
                    return block;
                }
                catch (Exception e)
                {
                    _logger?.Error(e, "Mining failed with exception.");
                    return null;
                }
            }
        }

        private async Task UpdateParentChainBlockInfo(ParentChainBlockInfo parentChainBlockInfo)
        {
            await _clientManager.UpdateParentChainBlockInfo(parentChainBlockInfo);
        }

        private void BroadcastBlock(IBlock block)
        {
            MessageHub.Instance.Publish(new BlockMinedMessage(block));
        }

        /// <summary>
        /// Generate a system tx for parent chain block info and broadcast it.
        /// </summary>
        /// <param name="parentChainBlockInfo"></param>
        /// <returns></returns>
        private async Task<Transaction> GenerateTransactionWithParentChainBlockInfo(ParentChainBlockInfo parentChainBlockInfo)
        {
            if (parentChainBlockInfo == null)
                return null; 
            try
            {
                var bn = await _blockChain.GetCurrentBlockHeightAsync();
                var bh = bn == 0 ? Hash.Genesis : (await _blockChain.GetHeaderByHeightAsync(bn)).GetHash();
                var bhPref = bh.Value.Where((x, i) => i < 4).ToArray();
                var tx = new Transaction
                {
                    From = _keyPair.GetAddress(),
                    To=Address.FromBytes(Hash.Xor(Config.ChainId,
                        Hash.FromString(SmartContractType.SideChainContract.ToString())).ToByteArray()),
//                    To = new Hash(Config.ChainId.CalculateHashWith(SmartContractType.SideChainContract.ToString())).ToAccount(),
                    RefBlockNumber = bn,
                    RefBlockPrefix = ByteString.CopyFrom(bhPref),
                    MethodName = "WriteParentChainBlockInfo",
                    P = ByteString.CopyFrom(_keyPair.GetEncodedPublicKey()),
                    Type = TransactionType.CrossChainBlockInfoTransaction,
                    Params = ByteString.CopyFrom(ParamsPacker.Pack(parentChainBlockInfo))
                };
                
                // sign tx
                var signature = new ECSigner().Sign(_keyPair, tx.GetHash().GetHashBytes());
                tx.R = ByteString.CopyFrom(signature.R);
                tx.S = ByteString.CopyFrom(signature.S);
                
                await BroadcastTransaction(tx);
                return tx;
            }
            catch (Exception e)
            {
                _logger?.Error(e, "PCB transaction generation failed.");
                return null;
            }
        }

        private async Task<bool> BroadcastTransaction(Transaction tx)
        {
            if (tx == null)
                return false;
            
            // insert to tx pool and broadcast
            var insertion = await _txPoolService.AddTxAsync(tx);
            if (insertion == TxValidation.TxInsertionAndBroadcastingError.Success)
            {
                MessageHub.Instance.Publish(new TransactionAddedToPool(tx));
                _logger.Debug($"Parent chain block info transaction insertion success. {tx.GetHash()}");
                return true;
            }
            _logger?.Debug($"Parent chain block info transaction insertion failed. {insertion}");
            return false;
        }
        
        /// <summary>
        /// Extract tx results from traces
        /// </summary>
        /// <param name="readyTxs"></param>
        /// <param name="traces"></param>
        /// <param name="executed"></param>
        /// <param name="rollback"></param>
        /// <param name="results"></param>
        private void ExtractTransactionResults(IEnumerable<Transaction> readyTxs, IEnumerable<TransactionTrace> traces,
            out List<Transaction> executed, out List<Transaction> rollback, out List<TransactionResult> results)
        {
            var canceledTxIds = new List<Hash>();
            results = new List<TransactionResult>();
            int index = 0;
            foreach (var trace in traces)
            {
                switch (trace.ExecutionStatus)
                {
                    case ExecutionStatus.Canceled:
                        // Put back transaction
                        canceledTxIds.Add(trace.TransactionId);
                        break;
                    case ExecutionStatus.ExecutedAndCommitted:
                        // Successful
                        var txRes = new TransactionResult()
                        {
                            TransactionId = trace.TransactionId,
                            Status = Status.Mined,
                            RetVal = ByteString.CopyFrom(trace.RetVal.ToFriendlyBytes()),
                            Index = index++
                        };
                        txRes.UpdateBloom();
                        results.Add(txRes);
                        break;
                    case ExecutionStatus.ContractError:
                        var txResF = new TransactionResult()
                        {
                            TransactionId = trace.TransactionId,
                            RetVal = ByteString.CopyFromUtf8(trace.StdErr), // Is this needed?
                            Status = Status.Failed,
                            Index = index++
                        };
                        results.Add(txResF);
                        break;
                    case ExecutionStatus.Undefined:
                        _logger?.Fatal(
                            $@"Transaction Id ""{
                                    trace.TransactionId
                                } is executed with status Undefined. Transaction trace: {trace}""");
                        break;
                    case ExecutionStatus.SystemError:
                        // SystemError shouldn't happen, and need to fix
                        _logger?.Fatal(
                            $@"Transaction Id ""{
                                    trace.TransactionId
                                } is executed with status SystemError. Transaction trace: {trace}""");
                        break;
                    case ExecutionStatus.ExecutedButNotCommitted:
                        // If this happens, there's problem with the code
                        _logger?.Fatal(
                            $@"Transaction Id ""{
                                    trace.TransactionId
                                } is executed with status ExecutedButNotCommitted. Transaction trace: {
                                    trace
                                }""");
                        break;
                }
            }

            var canceled = canceledTxIds.ToHashSet();
            executed = new List<Transaction>();
            rollback = new List<Transaction>();
            foreach (var tx in readyTxs)
            {
                if (canceled.Contains(tx.GetHash()))
                {
                    rollback.Add(tx);
                }
                else
                {
                    executed.Add(tx);
                }
            }
        }

        /// <summary>
        /// Update database
        /// </summary>
        /// <param name="executedTxs"></param>
        /// <param name="txResults"></param>
        /// <param name="block"></param>
        /// <param name="parentChainBlockInfo"></param>
        /// <param name="pcbTransaction"></param>
        private void Update(List<Transaction> executedTxs, List<TransactionResult> txResults, IBlock block, 
            ParentChainBlockInfo parentChainBlockInfo, Transaction pcbTransaction)
        {
            var bn = block.Header.Index;
            var bh = block.Header.GetHash();
            executedTxs.AsParallel().ForEach(async tx =>
                {
                    await _transactionManager.AddTransactionAsync(tx);
                    _txPoolService.RemoveAsync(tx.GetHash());
                });
            txResults.AsParallel().ForEach(async r =>
            {
                r.BlockNumber = bn;
                r.BlockHash = bh;
                r.MerklePath = block.Body.BinaryMerkleTree.GenerateMerklePath(r.Index);
                await _transactionResultManager.AddTransactionResultAsync(r);
                
                // update parent chain block info
                if (pcbTransaction != null && r.TransactionId.Equals(pcbTransaction.GetHash()) && r.Status.Equals(Status.Mined))
                {
                    await UpdateParentChainBlockInfo(parentChainBlockInfo);
                }
            });
            // update merkle tree
            _binaryMerkleTreeManager.AddTransactionsMerkleTreeAsync(block.Body.BinaryMerkleTree, Config.ChainId,
                block.Header.Index);
            _binaryMerkleTreeManager.AddSideChainTransactionRootsMerkleTreeAsync(
                block.Body.BinaryMerkleTreeForSideChainTransactionRoots, Config.ChainId, block.Header.Index);
        }

        /// <summary>
        /// Generate block
        /// </summary>
        /// <param name="chainId"></param>
        /// <param name="results"></param>
        /// <returns></returns>
        private async Task<IBlock> GenerateBlockAsync(Hash chainId, List<TransactionResult> results)
        {
            var blockChain = _chainService.GetBlockChain(chainId);

            var currentBlockHash = await blockChain.GetCurrentBlockHashAsync();
            var index = await blockChain.GetCurrentBlockHeightAsync() + 1;
            var block = new Block(currentBlockHash)
            {
                Header =
                {
                    Index = index,
                    ChainId = chainId,
                    Bloom = ByteString.CopyFrom(
                        Bloom.AndMultipleBloomBytes(
                            results.Where(x => !x.Bloom.IsEmpty).Select(x => x.Bloom.ToByteArray())
                        )
                    )
                }
            };

            // side chain info
            await CollectSideChainIndexedInfo(block);
            // add tx hash
            block.AddTransactions(results.Select(r => r.TransactionId));

            // set ws merkle tree root
            await _stateDictator.SetWorldStateAsync();
            var ws = await _stateDictator.GetLatestWorldStateAsync();
            block.Header.Time = Timestamp.FromDateTime(DateTime.UtcNow);

            if (ws != null)
            {
                block.Header.MerkleTreeRootOfWorldState = await ws.GetWorldStateMerkleTreeRootAsync();
            }

            // calculate and set tx merkle tree root 
            block.Complete();
            block.Sign(_keyPair);
            return block;
        }

        /// <summary>
        /// Generate block header
        /// </summary>
        /// <param name="chainId"></param>
        /// <param name="merkleTreeRootForTransaction"></param>
        /// <returns></returns>
        public async Task<IBlockHeader> GenerateBlockHeaderAsync(Hash chainId, Hash merkleTreeRootForTransaction)
        {
            // get ws merkle tree root
            var blockChain = _chainService.GetBlockChain(chainId);

            var lastBlockHash = await blockChain.GetCurrentBlockHashAsync();
            // TODO: Generic IBlockHeader
            var lastHeader = (BlockHeader) await blockChain.GetHeaderByHashAsync(lastBlockHash);
            var index = lastHeader.Index;
            var block = new Block(lastBlockHash);
            block.Header.Index = index + 1;
            block.Header.ChainId = chainId;

            var ws = await _stateDictator.GetWorldStateAsync(lastBlockHash);
            var state = await ws.GetWorldStateMerkleTreeRootAsync();

            var header = new BlockHeader
            {
                Version = 0,
                PreviousBlockHash = lastBlockHash,
                MerkleTreeRootOfWorldState = state,
                MerkleTreeRootOfTransactions = merkleTreeRootForTransaction
            };

            return header;
        }

        /// <summary>
        /// Side chains header info    
        /// </summary>
        /// <returns></returns>
        private async Task CollectSideChainIndexedInfo(IBlock block)
        {
            // interval waiting for each side chain
            var sideChainInfo = await _clientManager.CollectSideChainBlockInfo();
            block.Body.IndexedInfo.Add(sideChainInfo);
        }

        /// <summary>
        /// Get parent chain block info.
        /// </summary>
        /// <returns></returns>
        private async Task<ParentChainBlockInfo> GetParentChainBlockInfo()
        {
            var blocInfo = await _clientManager.CollectParentChainBlockInfo();
            return blocInfo;
        }
        
        /// <summary>
        /// Start mining
        /// init clients to side chain node 
        /// </summary>
        public void Init(ECKeyPair nodeKeyPair)
        {
            _timeoutMilliseconds = GlobalConfig.AElfMiningInterval;
            _keyPair = nodeKeyPair;
            _blockChain = _chainService.GetBlockChain(Config.ChainId);
            
            // start clients and server
            //_clientManager.CreateClientsToSideChain().Wait();
            //_sideChainServer.StartUp();
        }

        /// <summary>
        /// Stop mining
        /// </summary>
        public void Close()
        {
            _clientManager.CloseClientsToSideChain();
        }
    }
}