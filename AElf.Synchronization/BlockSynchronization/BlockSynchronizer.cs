using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using AElf.ChainController;
using AElf.ChainController.EventMessages;
using AElf.Common;
using AElf.Common.FSM;
using AElf.Configuration.Config.Chain;
using AElf.Kernel;
using AElf.Kernel.Types;
using AElf.Kernel.Types.Common;
using AElf.Miner.EventMessages;
using AElf.Synchronization.BlockExecution;
using AElf.Synchronization.EventMessages;
using Easy.MessageHub;
using NLog;

// ReSharper disable once CheckNamespace
namespace AElf.Synchronization.BlockSynchronization
{
    // ReSharper disable InconsistentNaming
    public class BlockSynchronizer : IBlockSynchronizer
    {
        // Some dependencies.
        private readonly IChainService _chainService;
        private readonly IBlockValidationService _blockValidationService;
        private readonly IBlockHeaderValidator _blockHeaderValidator;
        private readonly IBlockExecutor _blockExecutor;
        private readonly IBlockSet _blockSet;

        private IBlockChain _blockChain;

        private IBlockChain BlockChain => _blockChain ?? (_blockChain =
                                              _chainService.GetBlockChain(
                                                  Hash.LoadHex(ChainConfig.Instance.ChainId)));

        private readonly ILogger _logger;

        private readonly FSM<NodeState> _stateFSM;

        private static bool _terminated;

        public BlockSynchronizer(IChainService chainService, IBlockValidationService blockValidationService,
            IBlockExecutor blockExecutor, IBlockSet blockSet, IBlockHeaderValidator blockHeaderValidator)
        {
            _chainService = chainService;
            _blockValidationService = blockValidationService;
            _blockExecutor = blockExecutor;
            _blockSet = blockSet;
            _blockHeaderValidator = blockHeaderValidator;

            _stateFSM = new NodeStateFSM().Create();
            _stateFSM.CurrentState = NodeState.Catching;

            _logger = LogManager.GetLogger(nameof(BlockSynchronizer));

            _terminated = false;

            MessageHub.Instance.Subscribe<StateEvent>(e => { _stateFSM.ProcessWithStateEvent(e); });

            MessageHub.Instance.Subscribe<EnteringCatchingOrCaughtState>(async e => { await ReceiveNextValidBlock(); });
            MessageHub.Instance.Subscribe<EnteringRevertingState>(async e => { await HandleFork(); });

            MessageHub.Instance.Subscribe<HeadersReceived>(async inHeaders =>
            {
                if (inHeaders?.Headers == null || !inHeaders.Headers.Any())
                {
                    _logger?.Warn("Null headers or header list is empty.");
                    return;
                }

                var headers = inHeaders.Headers.OrderByDescending(h => h.Index).ToList();

                foreach (var blockHeader in headers)
                {
                    // Get previous block from the chain
                    var correspondingBlockHeader = await BlockChain.GetBlockByHeightAsync(blockHeader.Index - 1);

                    // If the hash of this previous block corresponds to "previous block hash" of the current header
                    // the link has been found
                    if (correspondingBlockHeader.BlockHashToHex == blockHeader.PreviousBlockHash.DumpHex())
                    {
                        // Launch header accepted event and return
                        MessageHub.Instance.Publish(new HeaderAccepted(blockHeader));
                        return;
                    }
                }

                // Launch unlinkable again with the last headers index 
                MessageHub.Instance.Publish(new UnlinkableHeader(headers.Last()));
            });

            MessageHub.Instance.Subscribe<DPoSStateChanged>(inState =>
            {
                if (inState.IsMining)
                {
                    MessageHub.Instance.Publish(StateEvent.MiningStart);
                }
                else
                {
                    MessageHub.Instance.Publish(StateEvent.MiningEnd);
                }
            });
            
            MessageHub.Instance.Subscribe<BlockMined>(inBlock =>
            {
                // Update DPoS process.
                MessageHub.Instance.Publish(UpdateConsensus.Update);
                AddMinedBlock(inBlock.Block); 
            });
            
            MessageHub.Instance.Subscribe<TerminationSignal>(signal =>
            {
                if (signal.Module == TerminatedModuleEnum.BlockSynchronizer)
                {
                    _terminated = true;
                    MessageHub.Instance.Publish(new TerminatedModule(TerminatedModuleEnum.BlockSynchronizer));
                }
            });
        }

        /// <summary>
        /// The entrance of block syncing.
        /// First step is to validate block header of this block.
        /// </summary>
        /// <param name="block"></param>
        /// <returns></returns>
        public async Task ReceiveBlock(IBlock block)
        {
            if (_terminated)
            {
                return;
            }

            if (!_blockSet.IsBlockReceived(block.GetHash(), block.Index))
            {
                _blockSet.AddBlock(block);
            }

            var blockHeaderValidationResult =
                await _blockHeaderValidator.ValidateBlockHeaderAsync(block.Header);
            
            _logger?.Trace($"BlockHeader validation result: {blockHeaderValidationResult.ToString()} - {block.BlockHashToHex}");

            if (blockHeaderValidationResult == BlockHeaderValidationResult.Success)
            {
                // Catching -> BlockValidating
                // Caught -> BlockValidating
                MessageHub.Instance.Publish(StateEvent.ValidBlockHeader);
                await HandleBlock(block);
            }

            if (blockHeaderValidationResult == BlockHeaderValidationResult.Unlinkable)
            {
                MessageHub.Instance.Publish(new UnlinkableHeader(block.Header));
            }

            if (blockHeaderValidationResult == BlockHeaderValidationResult.MaybeForked)
            {
                MessageHub.Instance.Publish(StateEvent.ForkDetected);
            }

            if (blockHeaderValidationResult == BlockHeaderValidationResult.Branched)
            {
                MessageHub.Instance.Publish(new LockMining(false));
            }
        }

        public async Task ReceiveNextValidBlock()
        {
            if (_stateFSM.CurrentState != NodeState.Catching && _stateFSM.CurrentState != NodeState.Caught)
            {
                IncorrectStateLog(nameof(ReceiveNextValidBlock));
                return;
            }
            
            var currentBlockHeight = await BlockChain.GetCurrentBlockHeightAsync();
            var nextBlocks = _blockSet.GetBlocksByHeight(currentBlockHeight + 1);
            foreach (var nextBlock in nextBlocks)
            {
                await ReceiveBlock(nextBlock);
                if (_stateFSM.CurrentState != NodeState.Catching && _stateFSM.CurrentState != NodeState.Caught)
                {
                    break;
                }
            }
        }

        private async Task HandleBlock(IBlock block)
        {
            if (_stateFSM.CurrentState != NodeState.BlockValidating)
            {
                IncorrectStateLog(nameof(HandleBlock));
                return;
            }
            
            _logger?.Trace("Entered HandleBlock");
            
            var blockValidationResult =
                await _blockValidationService.ValidateBlockAsync(block, await GetChainContextAsync());

            if (blockValidationResult.IsSuccess())
            {
                // BlockValidating -> BlockExecuting
                MessageHub.Instance.Publish(StateEvent.ValidBlock);
                await HandleValidBlock(block);
            }
            else
            {
                await HandleInvalidBlock(block, blockValidationResult);
            }
        }

        private async Task<BlockExecutionResult> HandleValidBlock(IBlock block)
        {
            if (_stateFSM.CurrentState != NodeState.BlockExecuting)
            {
                IncorrectStateLog(nameof(HandleValidBlock));
                return BlockExecutionResult.IncorrectNodeState;
            }
            
            var executionResult = await _blockExecutor.ExecuteBlock(block);

            _logger?.Trace($"Execution result of block {block.BlockHashToHex}: {executionResult}. Height *{block.Index}*");

            if (executionResult.CanExecuteAgain())
            {
                // BlockExecuting -> ExecutingLoop
                MessageHub.Instance.Publish(StateEvent.StateNotUpdated);
                await KeepExecutingBlocksOfHeight(block.Index);
                return BlockExecutionResult.InvalidSideChainInfo;
            }
            
            _blockSet.Tell(block);

            // Update the consensus information.
            MessageHub.Instance.Publish(UpdateConsensus.Update);
            
            // Notify the network layer the block has been executed.
            MessageHub.Instance.Publish(new BlockExecuted(block));
            
            // BlockAppending -> Catching / Caught
            MessageHub.Instance.Publish(StateEvent.BlockAppended);
            
            return BlockExecutionResult.Success;
        }

        private async Task HandleInvalidBlock(IBlock block, BlockValidationResult blockValidationResult)
        {
            _logger?.Warn(
                $"Invalid block {block.BlockHashToHex} : {blockValidationResult.ToString()}. Height: *{block.Index}*");

            MessageHub.Instance.Publish(new LockMining(false));

            // Handle the invalid blocks according to their validation results.
            if ((int) blockValidationResult < 100)
            {
                _blockSet.AddBlock(block);
            }
        }
        
        public void AddMinedBlock(IBlock block)
        {
            _blockSet.Tell(block);

            // We can say the "initial sync" is finished, set KeepHeight to a specific number
            if (_blockSet.KeepHeight == ulong.MaxValue)
            {
                _logger?.Trace(
                    $"Set the limit of the branched blocks cache in block set to {GlobalConfig.BlockCacheLimit}.");
                _blockSet.KeepHeight = GlobalConfig.BlockCacheLimit;
            }
        }

        private async Task HandleFork()
        {
            var currentHeight = await BlockChain.GetCurrentBlockHeightAsync();

            // Detect longest chain and switch.
            var forkHeight = _blockSet.AnyLongerValidChain(currentHeight - GlobalConfig.ForkDetectionLength);
            
            if (forkHeight != 0)
            {
                await RollbackToHeight(forkHeight, currentHeight - GlobalConfig.ForkDetectionLength);
            }
        }

        private async Task RollbackToHeight(ulong targetHeight, ulong currentHeight)
        {
            await BlockChain.RollbackToHeight(targetHeight - 1);
            _blockSet.InformRollback(targetHeight, currentHeight);
            MessageHub.Instance.Publish(StateEvent.RollbackFinished);
        }
        
        private async Task<IChainContext> GetChainContextAsync()
        {
            var chainId = Hash.LoadHex(ChainConfig.Instance.ChainId);
            var blockchain = _chainService.GetBlockChain(chainId);
            IChainContext chainContext = new ChainContext
            {
                ChainId = chainId,
                BlockHash = await blockchain.GetCurrentBlockHashAsync()
            };

            if (chainContext.BlockHash != Hash.Genesis && chainContext.BlockHash != null)
            {
                chainContext.BlockHeight =
                    ((BlockHeader) await blockchain.GetHeaderByHashAsync(chainContext.BlockHash)).Index;
            }

            return chainContext;
        }

        public IBlock GetBlockByHash(Hash blockHash)
        {
            return _blockSet.GetBlockByHash(blockHash) ?? BlockChain.GetBlockByHashAsync(blockHash).Result;
        }

        public async Task<BlockHeaderList> GetBlockHeaderList(ulong index, int count)
        {
            var blockHeaderList = new BlockHeaderList();
            for (var i = index; i > index - (ulong) count; i--)
            {
                var block = await BlockChain.GetBlockByHeightAsync(i);
                blockHeaderList.Headers.Add(block.Header);
            }

            return blockHeaderList;
        }

        private async Task KeepExecutingBlocksOfHeight(ulong height)
        {
            MessageHub.Instance.Publish(new LockMining(false));
            while (_stateFSM.CurrentState == NodeState.ExecutingLoop)
            {
                var blocks = _blockSet.GetBlocksByHeight(height).Where(b =>
                    _blockHeaderValidator.ValidateBlockHeaderAsync(b.Header).Result == BlockHeaderValidationResult.Success);
                foreach (var block in blocks)
                {
                    await _blockExecutor.ExecuteBlock(block);
                    Thread.Sleep(200);
                }

                if (_terminated)
                {
                    return;
                }
            }
        }

        private void IncorrectStateLog(string methodName)
        {
            _logger?.Trace($"Incorrect fsm state: {_stateFSM.CurrentState.ToString()} in method {methodName}");
        }
    }
}