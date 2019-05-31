using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AElf.Types;

namespace AElf.Kernel.SmartContract.Parallel
{
    public interface IResourceExtractionService
    {
        Task<IEnumerable<(Transaction, TransactionResourceInfo)>> GetResourcesAsync(IChainContext chainContext,
            IEnumerable<Transaction> transactions, CancellationToken ct);
    }
}