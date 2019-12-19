using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using AElf.Contracts.MultiToken;
using AElf.Kernel.Blockchain.Application;
using AElf.Kernel.SmartContract.Application;

[assembly: InternalsVisibleTo("AElf.Kernel.SmartContract.ExecutionPluginForAcs1.Tests")]
[assembly: InternalsVisibleTo("AElf.Kernel.SmartContract.ExecutionPluginForAcs8.Tests")]
namespace AElf.Kernel.TransactionPool.Application
{
    #region concrete strategys

    public abstract class CalculateCostStrategyBase
    {
        protected ICalculateAlgorithmService CalculateAlgorithmService { get; set; }

        public async Task<long> GetCostAsync(IChainContext chainContext, int cost)
        {
            if (chainContext != null)
                CalculateAlgorithmService.CalculateAlgorithmContext.BlockIndex = new BlockIndex
                {
                    BlockHash = chainContext.BlockHash,
                    BlockHeight = chainContext.BlockHeight
                };
            return await CalculateAlgorithmService.CalculateAsync(cost);
        }

        public void AddAlgorithm(BlockIndex blockIndex, IList<ICalculateWay> allWay)
        {
            CalculateAlgorithmService.CalculateAlgorithmContext.BlockIndex = blockIndex;
            CalculateAlgorithmService.AddAlgorithmByBlock(blockIndex, allWay);
        }

        public void RemoveForkCache(List<BlockIndex> blockIndexes)
        {
            CalculateAlgorithmService.RemoveForkCache(blockIndexes);
        }

        public void SetIrreversedCache(List<BlockIndex> blockIndexes)
        {
            CalculateAlgorithmService.SetIrreversedCache(blockIndexes);
        }
    }

    class CpuCalculateCostStrategy : CalculateCostStrategyBase, ICalculateCpuCostStrategy
    {
        public CpuCalculateCostStrategy(ITokenContractReaderFactory tokenStTokenContractReaderFactory,
            IBlockchainService blockchainService,
            IChainBlockLinkService chainBlockLinkService,
            ICalculateFunctionProvider functionProvider)
        {
            CalculateAlgorithmService =
                new CalculateAlgorithmService(tokenStTokenContractReaderFactory, blockchainService,
                    chainBlockLinkService, functionProvider);
                   
            CalculateAlgorithmService.CalculateAlgorithmContext.CalculateFeeTypeEnum = (int) FeeTypeEnum.Cpu;
        }
    }

    class StoCalculateCostStrategy : CalculateCostStrategyBase, ICalculateStoCostStrategy
    {
        public StoCalculateCostStrategy(ITokenContractReaderFactory tokenStTokenContractReaderFactory,
            IBlockchainService blockchainService,
            IChainBlockLinkService chainBlockLinkService,
            ICalculateFunctionProvider functionProvider)
        {
            CalculateAlgorithmService =
                new CalculateAlgorithmService(tokenStTokenContractReaderFactory, blockchainService,
                    chainBlockLinkService, functionProvider);
            
            CalculateAlgorithmService.CalculateAlgorithmContext.CalculateFeeTypeEnum = (int) FeeTypeEnum.Sto;
        }
    }

    class RamCalculateCostStrategy : CalculateCostStrategyBase, ICalculateRamCostStrategy
    {
        public RamCalculateCostStrategy(ITokenContractReaderFactory tokenStTokenContractReaderFactory,
            IBlockchainService blockchainService,
            IChainBlockLinkService chainBlockLinkService,
            ICalculateFunctionProvider functionProvider)
        {
            CalculateAlgorithmService =
                new CalculateAlgorithmService(tokenStTokenContractReaderFactory, blockchainService,
                    chainBlockLinkService, functionProvider);
            CalculateAlgorithmService.CalculateAlgorithmContext.CalculateFeeTypeEnum = (int) FeeTypeEnum.Ram;
        }
    }

    class NetCalculateCostStrategy : CalculateCostStrategyBase, ICalculateNetCostStrategy
    {
        public NetCalculateCostStrategy(ITokenContractReaderFactory tokenStTokenContractReaderFactory,
            IBlockchainService blockchainService,
            IChainBlockLinkService chainBlockLinkService,
            ICalculateFunctionProvider functionProvider)
        {
            CalculateAlgorithmService =
                new CalculateAlgorithmService(tokenStTokenContractReaderFactory, blockchainService,
                    chainBlockLinkService, functionProvider);
                    
            CalculateAlgorithmService.CalculateAlgorithmContext.CalculateFeeTypeEnum = (int) FeeTypeEnum.Net;
        }
    }

    internal class TxCalculateCostStrategy : CalculateCostStrategyBase, ICalculateTxCostStrategy
    {
        public TxCalculateCostStrategy(ITokenContractReaderFactory tokenStTokenContractReaderFactory,
            IBlockchainService blockchainService,
            IChainBlockLinkService chainBlockLinkService,
            ICalculateFunctionProvider functionProvider)
        {
            CalculateAlgorithmService =
                new CalculateAlgorithmService(tokenStTokenContractReaderFactory, blockchainService,
                        chainBlockLinkService, functionProvider);
            CalculateAlgorithmService.CalculateAlgorithmContext.CalculateFeeTypeEnum = (int) FeeTypeEnum.Tx;
        }
    }

    #endregion
}