using System;
using System.Linq;
using System.Threading.Tasks;
using AElf.Common;
using AElf.Kernel;
using AElf.Kernel.Blockchain.Application;
using AElf.Kernel.Blockchain.Domain;

namespace AElf.CrossChain
{
    public class CrossChainBlockExtraDataProvider : IBlockExtraDataProvider
    {
        private readonly ITransactionResultManager _transactionResultManager;

        public CrossChainBlockExtraDataProvider(ITransactionResultManager transactionResultManager)
        {
            _transactionResultManager = transactionResultManager;
        }

        public async Task FillExtraDataAsync(int chainId, Block block)
        {
            if (!CrossChainEventHelper.TryGetLogEventInBlock(block, out var interestedLogEvent))
                return;
            try
            {
                foreach (var txId in block.Body.Transactions)
                {
                    var res = await _transactionResultManager.GetTransactionResultAsync(txId);
                    
                    var sideChainTransactionsRoot =
                        CrossChainEventHelper.TryGetValidateCrossChainBlockData(res, block, interestedLogEvent, out _);
                    if (block.Header.BlockExtraData == null)
                    {
                        block.Header.BlockExtraData = new BlockExtraData();
                    }
                    block.Header.BlockExtraData.SideChainTransactionsRoot = sideChainTransactionsRoot;
                    return;
                }
            }
            catch (Exception)
            {
                // ignored
                // Deserialization/NULL value errors
            }
        }
    }
}