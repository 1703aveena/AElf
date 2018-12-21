﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AElf.ChainController;
using AElf.Common;
using AElf.Common.Attributes;
using AElf.Configuration;
using AElf.Configuration.Config.Chain;
using AElf.Configuration.Config.Consensus;
using AElf.Cryptography.ECDSA;
using AElf.Execution.Execution;
using AElf.Kernel;
using AElf.Kernel.Consensus;
using AElf.Kernel.Manager.Interfaces;
using AElf.Miner.EventMessages;
using AElf.Miner.Rpc.Client;
using AElf.Miner.Rpc.Exceptions;
using AElf.Miner.Rpc.Server;
using AElf.Miner.TxMemPool;
using AElf.Types.CSharp;
using Easy.MessageHub;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using NLog;
using NServiceKit.Common.Extensions;

namespace AElf.Miner.Miner
{
    [LoggerName(nameof(Miner))]
    public class Miner : IMiner
    {
        private int TimeoutMilliseconds => ConsensusConfig.Instance.DPoSMiningInterval / 4 * 3;
        
        private readonly ILogger _logger;
        private readonly ITxHub _txHub;
        private readonly IChainService _chainService;
        private readonly IExecutingService _executingService;
        private readonly ITransactionResultManager _transactionResultManager;
        private readonly IMerkleTreeManager _merkleTreeManager;
        private readonly IBlockValidationService _blockValidationService;
        private readonly IChainContextService _chainContextService;
        
        private IBlockChain _blockChain;
        
        private readonly ClientManager _clientManager;
        private readonly ServerManager _serverManager;

        private Address _producerAddress;
        private ECKeyPair _keyPair;
        private readonly IChainManager _chainManager;
        private readonly ConsensusDataProvider _consensusDataProvider;

        private IMinerConfig Config { get; }

        private TransactionFilter _txFilter;

        private readonly double _maxMineTime;

        public Miner(IMinerConfig config, ITxHub txHub, IChainService chainService,
            IExecutingService executingService, ITransactionResultManager transactionResultManager,
            ILogger logger, ClientManager clientManager,
            IMerkleTreeManager merkleTreeManager, ServerManager serverManager,
            IBlockValidationService blockValidationService, IChainContextService chainContextService
            , IChainManager chainManager,IStateManager stateManager)
        {
            _txHub = txHub;
            _chainService = chainService;
            _executingService = executingService;
            _transactionResultManager = transactionResultManager;
            _logger = logger;
            _clientManager = clientManager;
            _merkleTreeManager = merkleTreeManager;
            _serverManager = serverManager;
            _blockValidationService = blockValidationService;
            _chainContextService = chainContextService;
            
            Config = config;
            
            _consensusDataProvider = new ConsensusDataProvider(stateManager);

            _maxMineTime = ConsensusConfig.Instance.DPoSMiningInterval * NodeConfig.Instance.RatioMine;
        }
        
        /// <summary>
        /// Initializes the mining with the producers key pair.
        /// </summary>
        public void Init(ECKeyPair nodeKeyPair)
        {
            _txFilter = new TransactionFilter();
            
            _keyPair = NodeConfig.Instance.ECKeyPair;
            _producerAddress = Address.Parse(NodeConfig.Instance.NodeAccount);
            
            _blockChain = _chainService.GetBlockChain(Config.ChainId);
        }

        /// <inheritdoc />
        /// <summary>
        /// Mine process.
        /// </summary>
        /// <returns></returns>
        public async Task<IBlock> Mine()
        {
            try
            {
                var stopwatch = new Stopwatch();
                stopwatch.Start();
                await GenerateTransactionWithParentChainBlockInfo();
                var txs = await _txHub.GetReceiptsOfExecutablesAsync();
                var txGrp = txs.GroupBy(tr => tr.IsSystemTxn).ToDictionary(x => x.Key, x => x.ToList());
                var traces = new List<TransactionTrace>();
                //ParentChainBlockInfo pcb = null; 
                if (txGrp.TryGetValue(true, out var sysRcpts))
                {

                    var sysTxs = sysRcpts.Select(x => x.Transaction).ToList();
                    _txFilter.Execute(sysTxs);

                    _logger?.Trace($"Start executing {sysTxs.Count} system transactions.");
                    traces = await ExecuteTransactions(sysTxs, true, TransactionType.DposTransaction);
                    _logger?.Trace($"Finish executing {sysTxs.Count} system transactions.");

                    // need check result of cross chain transaction 
                    //FindCrossChainInfo(sysTxs, traces, out pcb);
                }
                if (txGrp.TryGetValue(false, out var regRcpts))
                {
                    var contractZeroAddress = ContractHelpers.GetGenesisBasicContractAddress(Config.ChainId);
                    var regTxs = new List<Transaction>();
                    var contractTxs = new List<Transaction>();

                    foreach (var regRcpt in regRcpts)
                    {
                        if (regRcpt.Transaction.To.Equals(contractZeroAddress))
                        {
                            contractTxs.Add(regRcpt.Transaction);
                        }
                        else
                        {
                            regTxs.Add(regRcpt.Transaction);
                        }
                    }
                    
                    _logger?.Trace($"Start executing {regTxs.Count} regular transactions.");
                    traces.AddRange(await ExecuteTransactions(regTxs));
                    _logger?.Trace($"Finish executing {regTxs.Count} regular transactions.");
                    
                    _logger?.Trace($"Start executing {contractTxs.Count} contract transactions.");
                    traces.AddRange(await ExecuteTransactions(contractTxs, transactionType: TransactionType.ContractDeployTransaction));
                    _logger?.Trace($"Finish executing {contractTxs.Count} contract transactions.");
                }

                ExtractTransactionResults(traces, out var results);

                // generate block
                var block = await GenerateBlockAsync(results);
                _logger?.Info($"Generated block {block.BlockHashToHex} at height {block.Header.Index} with {block.Body.TransactionsCount} txs.");

                // validate block before appending
                var chainContext = await _chainContextService.GetChainContextAsync(Hash.LoadBase58(ChainConfig.Instance.ChainId));
                var blockValidationResult = await _blockValidationService.ValidateBlockAsync(block, chainContext);
                if (blockValidationResult != BlockValidationResult.Success)
                {
                    _logger?.Warn($"Found the block generated before invalid: {blockValidationResult}.");
                    return null;
                }
                // append block
                await _blockChain.AddBlocksAsync(new List<IBlock> {block});

                MessageHub.Instance.Publish(new BlockMined(block));

                // insert to db
                Update(results, block);
                /*if (pcb != null)
                {
                    await _chainManagerBasic.UpdateCurrentBlockHeightAsync(pcb.ChainId, pcb.Height);
                }*/
                await _txHub.OnNewBlock((Block)block);
                MessageHub.Instance.Publish(new BlockMinedAndStored(block));
                stopwatch.Stop();
                _logger?.Info($"Generate block {block.BlockHashToHex} at height {block.Header.Index} " +
                              $"with {block.Body.TransactionsCount} txs, duration {stopwatch.ElapsedMilliseconds} ms.");

                return block;
            }
            catch (Exception e)
            {
                _logger?.Error(e, "Mining failed with exception.");
                return null;
            }
        }

        private async Task<List<TransactionTrace>> ExecuteTransactions(List<Transaction> txs, bool noTimeout = false,
            TransactionType transactionType = TransactionType.ContractTransaction)
        {
            using (var cts = new CancellationTokenSource())
            {
                if (!noTimeout)
                {
                    var distance = await _consensusDataProvider.GetDistanceToTimeSlotEnd();
                    var distanceRation = distance * (NodeConfig.Instance.RatioSynchronize + NodeConfig.Instance.RatioMine);
                    var timeout = Math.Min(distanceRation, _maxMineTime);
                    cts.CancelAfter(TimeSpan.FromMilliseconds(timeout));
                    _logger?.Trace($"Execution limit time: {timeout}ms");
                }

                if (cts.IsCancellationRequested)
                    return null;
                var disambiguationHash =
                    HashHelpers.GetDisambiguationHash(await GetNewBlockIndexAsync(), Hash.FromRawBytes(_keyPair.PublicKey));

                var traces = txs.Count == 0
                    ? new List<TransactionTrace>()
                    : await _executingService.ExecuteAsync(txs, Config.ChainId, cts.Token, disambiguationHash,transactionType);

                return traces;
            }
        }

        private async Task<ulong> GetNewBlockIndexAsync()
        {
            var blockChain = _chainService.GetBlockChain(Config.ChainId);
            var index = await blockChain.GetCurrentBlockHeightAsync() + 1;
            return index;
        }

        /// <summary>
        /// Generate a system tx for parent chain block info and broadcast it.
        /// </summary>
        /// <returns></returns>
        private async Task GenerateTransactionWithParentChainBlockInfo()
        {
            var parentChainBlockInfo = GetParentChainBlockInfo();
            if (parentChainBlockInfo == null)
                return;
            try
            {
                var bn = await _blockChain.GetCurrentBlockHeightAsync();
                bn = bn > 4 ? bn - 4 : 0;
                var bh = bn == 0 ? Hash.Genesis : (await _blockChain.GetHeaderByHeightAsync(bn)).GetHash();
                var bhPref = bh.Value.Where((x, i) => i < 4).ToArray();
                var tx = new Transaction
                {
                    From = _producerAddress,
                    To = ContractHelpers.GetCrossChainContractAddress(Config.ChainId),
                    RefBlockNumber = bn,
                    RefBlockPrefix = ByteString.CopyFrom(bhPref),
                    MethodName = "WriteParentChainBlockInfo",
                    Type = TransactionType.CrossChainBlockInfoTransaction,
                    Params = ByteString.CopyFrom(ParamsPacker.Pack(parentChainBlockInfo)),
                    Time = Timestamp.FromDateTime(DateTime.UtcNow)
                };
                
                // sign tx
                var signature = new ECSigner().Sign(_keyPair, tx.GetHash().DumpByteArray());
                tx.Sigs.Add(ByteString.CopyFrom(signature.SigBytes));

                await InsertTransactionToPool(tx);
                _logger?.Trace($"Generated Cross chain info transaction {tx.GetHash()}");
            }
            catch (Exception e)
            {
                _logger?.Error(e, "PCB transaction generation failed.");
            }
        }

        private async Task InsertTransactionToPool(Transaction tx)
        {
            if (tx == null)
                return;
            // insert to tx pool and broadcast
            await _txHub.AddTransactionAsync(tx, skipValidation:true).ConfigureAwait(false);
        }

        /// <summary>
        /// Extract tx results from traces
        /// </summary>
        /// <param name="traces"></param>
        /// <param name="results"></param>
        private void ExtractTransactionResults(IEnumerable<TransactionTrace> traces, out HashSet<TransactionResult> results)
        {
            results = new HashSet<TransactionResult>();
            try
            {
                int index = 0;
                foreach (var trace in traces)
                {
                    switch (trace.ExecutionStatus)
                    {
                        case ExecutionStatus.Canceled:
                            // Put back transaction
                            break;
                        case ExecutionStatus.ExecutedAndCommitted:
                            // Successful
                            var txRes = new TransactionResult()
                            {
                                TransactionId = trace.TransactionId,
                                Status = Status.Mined,
                                RetVal = ByteString.CopyFrom(trace.RetVal.ToFriendlyBytes()),
                                StateHash = trace.GetSummarizedStateHash(),
                                Index = index++
                            };
                            txRes.UpdateBloom();

                            // insert deferred txn to transaction pool and wait for execution 
                            if (trace.DeferredTransaction.Length != 0)
                            {
                                var deferredTxn = Transaction.Parser.ParseFrom(trace.DeferredTransaction);
                                InsertTransactionToPool(deferredTxn).ConfigureAwait(false);
                                txRes.DeferredTxnId = deferredTxn.GetHash();
                            }

                            results.Add(txRes);
                            break;
                        case ExecutionStatus.ContractError:
                            var txResF = new TransactionResult()
                            {
                                TransactionId = trace.TransactionId,
                                RetVal = ByteString.CopyFromUtf8(trace.StdErr), // Is this needed?
                                Status = Status.Failed,
                                StateHash = Hash.Default,
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
            }
            catch (Exception e)
            {
                _logger?.Trace(e, "Error in ExtractTransactionResults");
            }
        }


        /// <summary>
        /// Get <see cref="ParentChainBlockInfo"/> from executed transaction
        /// </summary>
        /// <param name="sysTxns">Executed transactions.</param>
        /// <param name="traces"></param>
        /// <param name="parentChainBlockInfo"></param>
        private void FindCrossChainInfo(List<Transaction> sysTxns, List<TransactionTrace> traces, out ParentChainBlockInfo parentChainBlockInfo)
        {
            parentChainBlockInfo = null;
            var crossChainTx =
                sysTxns.FirstOrDefault(t => t.Type == TransactionType.CrossChainBlockInfoTransaction);
            if (crossChainTx == null)
                return;
            
            var trace = traces.FirstOrDefault(t => t.TransactionId.Equals(crossChainTx.GetHash()));
            if (trace == null || trace.ExecutionStatus != ExecutionStatus.ExecutedAndCommitted)
                return;
            parentChainBlockInfo = (ParentChainBlockInfo) ParamsPacker.Unpack(crossChainTx.Params.ToByteArray(),
                new[] {typeof(ParentChainBlockInfo)})[0];
        }
        
        /// <summary>
        /// Update database
        /// </summary>
        /// <param name="txResults"></param>
        /// <param name="block"></param>
        private void Update(HashSet<TransactionResult> txResults, IBlock block)
        {
            var bn = block.Header.Index;
            var bh = block.Header.GetHash();
            txResults.AsParallel().ForEach(async r =>
            {
                r.BlockNumber = bn;
                r.BlockHash = bh;
                r.MerklePath = block.Body.BinaryMerkleTree.GenerateMerklePath(r.Index);
                await _transactionResultManager.AddTransactionResultAsync(r);
            });
            // update merkle tree
            _merkleTreeManager.AddTransactionsMerkleTreeAsync(block.Body.BinaryMerkleTree, Config.ChainId,
                block.Header.Index);
            if (block.Body.IndexedInfo.Count > 0)
                _merkleTreeManager.AddSideChainTransactionRootsMerkleTreeAsync(
                    block.Body.BinaryMerkleTreeForSideChainTransactionRoots, Config.ChainId, block.Header.Index);
        }

        /// <summary>
        /// Generate block
        /// </summary>
        /// <param name="results"></param>
        /// <returns></returns>
        private async Task<IBlock> GenerateBlockAsync(HashSet<TransactionResult> results)
        {
            var blockChain = _chainService.GetBlockChain(Config.ChainId);

            var currentBlockHash = await blockChain.GetCurrentBlockHashAsync();
            var index = await blockChain.GetCurrentBlockHeightAsync() + 1;
            var block = new Block(currentBlockHash)
            {
                Header =
                {
                    Index = index,
                    ChainId = Config.ChainId,
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
            block.AddTransactions(results.Select(x => x.TransactionId));

            // set ws merkle tree root
            block.Header.MerkleTreeRootOfWorldState =
                new BinaryMerkleTree().AddNodes(results.Select(x => x.StateHash)).ComputeRootHash();
            block.Header.Time = Timestamp.FromDateTime(DateTime.UtcNow);

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
            var block = new Block(lastBlockHash) {Header = {Index = index + 1, ChainId = chainId}};

//            var ws = await _stateDictator.GetWorldStateAsync(lastBlockHash);
//            var state = await ws.GetWorldStateMerkleTreeRootAsync();

            var header = new BlockHeader
            {
                Version = 0,
                PreviousBlockHash = lastBlockHash,
                MerkleTreeRootOfWorldState = Hash.Default,
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
        private ParentChainBlockInfo GetParentChainBlockInfo()
        {
            try
            {
                var blocInfo = _clientManager.TryGetParentChainBlockInfo();
                return blocInfo;
            }
            catch (Exception e)
            {
                if (e is ClientShutDownException)
                    return null;
                throw;
            }
        }

        /// <summary>
        /// Stop mining
        /// </summary>
        public void Close()
        {
            _clientManager.CloseClientsToSideChain();
            _serverManager.Close();
        }
    }
}