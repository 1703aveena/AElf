﻿using System;
using System.Threading;
using System.Threading.Tasks;
using AElf.ChainController;
using AElf.Configuration;
using AElf.Cryptography.ECDSA;
using AElf.Kernel.Node.Protocol;
using AElf.SmartContract;
using NLog;

namespace AElf.Kernel.Node
{
    public class MinerHelper
    {
        private readonly ILogger _logger;
        private readonly ITxPoolService _txPoolService;
        private readonly INodeConfig _nodeConfig;
        private readonly IWorldStateDictator _worldStateDictator;
        private readonly IChainContextService _chainContextService;
        private readonly IBlockVaildationService _blockVaildationService;
        private readonly IChainService _chainService;
        private readonly IBlockExecutor _blockExecutor;
        public int IsMiningInProcess => _flag;
        private int _flag;

        private MainChainNode Node { get; }

        private ECKeyPair NodeKeyPair
        {
            get => Node.NodeKeyPair;
        }

        private readonly IMiner _miner;
        private readonly Consensus _consensus;
        private readonly IBlockSynchronizer _synchronizer;

        public MinerHelper(ILogger logger, MainChainNode node,
            ITxPoolService txPoolService,
            INodeConfig nodeConfig,
            IWorldStateDictator worldStateDictator,
            IBlockExecutor blockExecutor,
            IChainService chainService,
            IChainContextService chainContextService, IBlockVaildationService blockVaildationService,
            IMiner miner, Consensus consensus, IBlockSynchronizer synchronizer)
        {
            _logger = logger;
            Node = node;
            _txPoolService = txPoolService;
            _nodeConfig = nodeConfig;
            _worldStateDictator = worldStateDictator;
            _blockExecutor = blockExecutor;
            _chainService = chainService;
            _chainContextService = chainContextService;
            _blockVaildationService = blockVaildationService;
            _miner = miner;
            _consensus = consensus;
            _synchronizer = synchronizer;
        }

        public async Task<IBlock> Mine()
        {
            var res = Interlocked.CompareExchange(ref _flag, 1, 0);
            if (res == 1)
                return null;
            try
            {
                _logger?.Trace($"Mine - Entered mining {res}");

                _worldStateDictator.BlockProducerAccountAddress = NodeKeyPair.GetAddress();

                var task = Task.Run(async () => await _miner.Mine());

                if (!task.Wait(TimeSpan.FromMilliseconds(Globals.AElfDPoSMiningInterval * 0.9)))
                {
                    _logger?.Error("Mining timeout.");
                    return null;
                }

                var b = Interlocked.CompareExchange(ref _flag, 0, 1);

                _synchronizer.IncrementChainHeight();

                _logger?.Trace($"Mine - Leaving mining {b}");

                Task.WaitAll();

                //Update DPoS observables.
                //Sometimes failed to update this observables list (which is weird), just ignore this.
                //Which means this node will do nothing in this round.
                try
                {
                    await Node.CheckUpdatingConsensusProcess();
                }
                catch (Exception e)
                {
                    _logger?.Error(e, "Somehow failed to update DPoS observables. Will recover soon.");
                    //In case just config one node to produce blocks.
                    _consensus.AElfDPoSObserver.RecoverMining();
                }

                return task.Result;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                Interlocked.CompareExchange(ref _flag, 0, 1);
                return null;
            }
        }

        /// <summary>
        /// Add a new block received from network by first validating it and then
        /// executing it.
        /// </summary>
        /// <param name="block"></param>
        /// <returns></returns>
        public async Task<BlockExecutionResult> ExecuteAndAddBlock(IBlock block)
        {
            try
            {
                var res = Interlocked.CompareExchange(ref _flag, 1, 0);
                if (res == 1)
                    return new BlockExecutionResult(false, ValidationError.Mining);

                var context = await _chainContextService.GetChainContextAsync(_nodeConfig.ChainId);
                var error = await _blockVaildationService.ValidateBlockAsync(block, context, NodeKeyPair);

                if (error != ValidationError.Success)
                {
                    var blockchain = _chainService.GetBlockChain(_nodeConfig.ChainId);
                    var localCorrespondingBlock = await blockchain.GetBlockByHeightAsync(block.Header.Index);
                    if (error == ValidationError.OrphanBlock)
                    {
                        //TODO: limit the count of blocks to rollback
                        if (block.Header.Time.ToDateTime() < localCorrespondingBlock.Header.Time.ToDateTime())
                        {
                            _logger?.Trace("Ready to rollback");
                            //Rollback world state
                            var txs = await _worldStateDictator.RollbackToSpecificHeight(block.Header.Index);

                            await _txPoolService.RollBack(txs);
                            _worldStateDictator.PreBlockHash = block.Header.PreviousBlockHash;
                            await _worldStateDictator.RollbackCurrentChangesAsync();

                            var ws = await _worldStateDictator.GetWorldStateAsync(block.GetHash());
                            _logger?.Trace(
                                $"Current world state {(await ws.GetWorldStateMerkleTreeRootAsync()).ToHex()}");

                            error = ValidationError.Success;
                        }
                        else
                        {
                            // insert to database 
                            Interlocked.CompareExchange(ref _flag, 0, 1);
                            return new BlockExecutionResult(false, ValidationError.OrphanBlock);
                        }
                    }
                    else
                    {
                        Interlocked.CompareExchange(ref _flag, 0, 1);
                        _logger?.Trace("Invalid block received from network: " + error);
                        return new BlockExecutionResult(false, error);
                    }
                }

                var executed = await _blockExecutor.ExecuteBlock(block);
                Interlocked.CompareExchange(ref _flag, 0, 1);

                Task.WaitAll();
                await Node.CheckUpdatingConsensusProcess();

                return new BlockExecutionResult(executed, error);
                //return new BlockExecutionResult(true, error);
            }
            catch (Exception e)
            {
                _logger?.Error(e, "Block synchronzing failed");
                Interlocked.CompareExchange(ref _flag, 0, 1);
                return new BlockExecutionResult(e);
            }
        }
    }
}