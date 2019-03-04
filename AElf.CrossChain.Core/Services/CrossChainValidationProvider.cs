using System;
using System.Linq;
using System.Threading.Tasks;
using AElf.Common;
using AElf.Cryptography;
using AElf.Kernel;
using AElf.Kernel.Blockchain.Application;
using AElf.Kernel.Blockchain.Domain;

namespace AElf.CrossChain
{
    public class CrossChainValidationProvider : IBlockValidationProvider
    {
        private readonly ICrossChainService _crossChainService;
        private readonly ICrossChainContractReader _crossChainContractReader;

        public CrossChainValidationProvider(ICrossChainService crossChainService, 
            ICrossChainContractReader crossChainContractReader)
        {
            _crossChainService = crossChainService;
            _crossChainContractReader = crossChainContractReader;
        }

        public Task<bool> ValidateBlockBeforeExecuteAsync(IBlock block)
        {
            // nothing to validate before execution for cross chain
            return Task.FromResult(true);
        }

        public async Task<bool> ValidateBlockAfterExecuteAsync(IBlock block)
        {
            try
            {
//                if (!CrossChainEventHelper.TryGetLogEventInBlock(block, out var logEvent) ||
//                    await ValidateCrossChainLogEventInBlock(logEvent, block))
//                    return true; // no event means no indexing.
//                throw new Exception();
                var indexedCrossChainBlockData =
                    await _crossChainContractReader.GetCrossChainBlockDataAsync(block.GetHash(), block.Height);
                if (indexedCrossChainBlockData == null)
                    return true;
                return await ValidateCrossChainBlockDataAsync(indexedCrossChainBlockData, block.Header.BlockExtraData.SideChainTransactionsRoot,
                    block.GetHash(), block.Height);
            }
            catch (Exception e)
            {
                throw new ValidateNextTimeBlockValidationException("Cross chain validation failed after execution.", e);
            }
        }

//        private async Task<bool> ValidateCrossChainLogEventInBlock(LogEvent interestedLogEvent, IBlock block)
//        {
//            foreach (var txId in block.Body.Transactions)
//            {
//                var res = await _transactionResultManager.GetTransactionResultAsync(txId);
//                var sideChainTransactionsRoot =
//                    CrossChainEventHelper.TryGetValidateCrossChainBlockData(res, block, interestedLogEvent,
//                        out var crossChainBlockData);
//                // first check equality with the root in header
//                if(sideChainTransactionsRoot == null 
//                   || !sideChainTransactionsRoot.Equals(block.Header.BlockExtraData.SideChainTransactionsRoot))
//                    continue;
//                return await ValidateCrossChainBlockDataAsync(crossChainBlockData,
//                    block.Header.GetHash(), block.Header.Height);
//            }
//            return false;
//        }

        private async Task<bool> ValidateCrossChainBlockDataAsync(CrossChainBlockData crossChainBlockData, Hash sideChainTransactionsRoot,
            Hash preBlockHash, ulong preBlockHeight)
        {
            var txRootHashList = crossChainBlockData.ParentChainBlockData.Select(pcb => pcb.Root.SideChainTransactionsRoot).ToList();
            var calculatedSideChainTransactionsRoot = new BinaryMerkleTree().AddNodes(txRootHashList).ComputeRootHash();
            
            // first check equality with the root in header
            if (sideChainTransactionsRoot != null && !calculatedSideChainTransactionsRoot.Equals(sideChainTransactionsRoot))
                return false;
            
            return await _crossChainService.ValidateSideChainBlockDataAsync(
                       crossChainBlockData.SideChainBlockData, preBlockHash, preBlockHeight) &&
                   await _crossChainService.ValidateParentChainBlockDataAsync(
                       crossChainBlockData.ParentChainBlockData, preBlockHash, preBlockHeight);
        }
    }
}