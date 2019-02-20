using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf.Common;
using AElf.Kernel.Blockchain.Domain;
using AElf.Kernel.Blockchain.Events;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Local;
using IChainManager = AElf.Kernel.Blockchain.Domain.IChainManager;

namespace AElf.Kernel.Blockchain.Application
{
    public interface IBlockchainService
    {
        Task AddBlockAsync(int chainId, Block block);
        Task<bool> HasBlockAsync(int chainId, Hash blockId);
        Task<List<ChainBlockLink>> AttachBlockToChainAsync(Chain chain, Block block);
        Task<Block> GetBlockByHashAsync(int chainId, Hash blockId);
        Task<Chain> GetChainAsync(int chainId);

        Task<Chain> CreateChainAsync(int chainId, Block block);

        Task<Hash> GetBlockHashByHeightAsync(Chain chain, ulong height, Hash currentBlockHash = null);
    }

    public interface ILightBlockchainService : IBlockchainService
    {
    }

    /*
    public class LightBlockchainService : ILightBlockchainService
    {
        public async Task<bool> AddBlockAsync(int chainId, Block block)
        {
            throw new System.NotImplementedException();
        }

        public async Task<bool> HasBlockAsync(int chainId, Hash blockId)
        {
            throw new System.NotImplementedException();
        }

        public async Task<List<ChainBlockLink>> AddBlocksAsync(int chainId, IEnumerable<Block> blocks)
        {
            throw new System.NotImplementedException();
        }

        public async Task<Block> GetBlockByHashAsync(int chainId, Hash blockId)
        {
            throw new System.NotImplementedException();
        }

        public async Task<Chain> GetChainAsync(int chainId)
        {
            throw new System.NotImplementedException();
        }
    }*/

    public interface IFullBlockchainService : IBlockchainService
    {
    }

    public class FullBlockchainService : IFullBlockchainService, ITransientDependency
    {
        private readonly IChainManager _chainManager;
        private readonly IBlockManager _blockManager;
        private readonly IBlockExecutingService _blockExecutingService;
        private readonly ITransactionManager _transactionManager;
        private readonly IBlockValidationService _blockValidationService;
        public ILocalEventBus LocalEventBus { get; set; }

        public FullBlockchainService(IChainManager chainManager, IBlockManager blockManager,
            IBlockExecutingService blockExecutingService, ITransactionManager transactionManager, 
            IBlockValidationService blockValidationService)
        {
            _chainManager = chainManager;
            _blockManager = blockManager;
            _blockExecutingService = blockExecutingService;
            _transactionManager = transactionManager;
            _blockValidationService = blockValidationService;
            LocalEventBus = NullLocalEventBus.Instance;
        }

        public async Task AddBlockAsync(int chainId, Block block)
        {
            await _blockManager.AddBlockHeaderAsync(block.Header);
            if (block.Body.TransactionList.Count > 0)
            {
                foreach (var transaction in block.Body.TransactionList)
                {
                    await _transactionManager.AddTransactionAsync(transaction);
                }
            }
            
            await _blockManager.AddBlockBodyAsync(block.Header.GetHash(), block.Body);
        }

        public async Task<bool> HasBlockAsync(int chainId, Hash blockId)
        {
            return (await _blockManager.GetBlockHeaderAsync(blockId)) != null;
        }

        public async Task<List<ChainBlockLink>> AttachBlockToChainAsync(Chain chain, Block block)
        {
            var status = await _chainManager.AttachBlockToChainAsync(chain, new ChainBlockLink()
            {
                Height = block.Header.Height,
                BlockHash = block.Header.GetHash(),
                PreviousBlockHash = block.Header.PreviousBlockHash
            });
            
            List<ChainBlockLink> blockLinks = null;

            if (status.HasFlag(BlockAttachOperationStatus.NewBlockLinked))
            {
                blockLinks = await _chainManager.GetNotExecutedBlocks(chain.Id, block.Header.GetHash());

                foreach (var blockLink in blockLinks)
                {
                    if (!await _blockValidationService.ValidateBlockBeforeExecuteAsync(chain.Id, block))
                    {
                        break;
                    }

                    await ExecuteBlock(chain.Id, blockLink);

                    if (!await _blockValidationService.ValidateBlockAfterExecuteAsync(chain.Id, block))
                    {
                        break;
                    }
                    
                    // TODO: Set valid block
                }
                
                // TODO: set best chain and valid

                if (status.HasFlag(BlockAttachOperationStatus.LongestChainFound))
                {
                    await LocalEventBus.PublishAsync(
                        new BestChainFoundEvent()
                        {
                            ChainId = chain.Id,
                            BlockHash = chain.BestChainHash,
                            BlockHeight = chain.BestChainHeight
                        });
                }
            }


            return blockLinks;
        }

        private async Task ExecuteBlock(int chainId, ChainBlockLink blockLink)
        {
            await _blockExecutingService.ExecuteBlockAsync(chainId, blockLink.BlockHash);
            await _chainManager.SetChainBlockLinkAsExecuted(chainId, blockLink);
        }


        public async Task<Block> GetBlockByHashAsync(int chainId, Hash blockId)
        {
            return await _blockManager.GetBlockAsync(blockId);
        }

        public async Task<Chain> GetChainAsync(int chainId)
        {
            return await _chainManager.GetAsync(chainId);
        }

        public async Task<Chain> CreateChainAsync(int chainId, Block block)
        {
            await AddBlockAsync(chainId, block);
            return await _chainManager.CreateAsync(chainId, block.GetHash());
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chain"></param>
        /// <param name="height"></param>
        /// <param name="currentBlockHash">the search beginning of block, if it is null, it will start at best chain block</param>
        /// <returns></returns>
        public async Task<Hash> GetBlockHashByHeightAsync(Chain chain, ulong height, Hash currentBlockHash = null)
        {
            if (chain.LastIrreversibleBlockHeight <= height)
            {
                return (await _chainManager.GetChainBlockIndexAsync(chain.Id, chain.LastIrreversibleBlockHeight)).BlockHash;
            }

            if (currentBlockHash == null)
                currentBlockHash = chain.BestChainHash;
            
            //TODO: may introduce cache to improve the performance

            var chainBlockLink = await _chainManager.GetChainBlockLinkAsync(chain.Id, currentBlockHash);
            if (chainBlockLink.Height < height)
                return null;
            
            while (true)
            {
                if (chainBlockLink.Height == height)
                    return chainBlockLink.BlockHash;
                currentBlockHash = chainBlockLink.PreviousBlockHash;
                chainBlockLink = await _chainManager.GetChainBlockLinkAsync(chain.Id, currentBlockHash);
            }
        }
    }
}