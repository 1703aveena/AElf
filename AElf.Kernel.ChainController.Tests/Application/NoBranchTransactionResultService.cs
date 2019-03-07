using System.Threading.Tasks;
using AElf.Common;
using AElf.Kernel.Blockchain.Application;
using AElf.Kernel.Blockchain.Domain;

namespace AElf.Kernel.ChainController.Application
{
    public class NoBranchTransactionResultService //: ITransactionResultService
    {
        private readonly Hash _nullDisambiguationHash = Hash.Ones;
        private readonly ITransactionResultManager _transactionResultManager;
        public NoBranchTransactionResultService(ITransactionResultManager transactionResultManager)
        {
            _transactionResultManager = transactionResultManager;
        }
        public async Task AddTransactionResultAsync(TransactionResult transactionResult, BlockHeader blockHeader)
        {
            await _transactionResultManager.AddTransactionResultAsync(transactionResult, _nullDisambiguationHash);
        }

        public async Task<TransactionResult> GetTransactionResultAsync(Hash transactionId)
        {
            return await _transactionResultManager.GetTransactionResultAsync(transactionId, _nullDisambiguationHash);
        }
    }
}