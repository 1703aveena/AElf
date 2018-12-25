using System.Threading.Tasks;
using AElf.Common;
using AElf.Kernel.Manager.Interfaces;
using AElf.Kernel.Storage;

namespace AElf.Kernel.Manager.Managers
{
    public class TransactionTraceManager : ITransactionTraceManager
    {
        private readonly IKeyValueStore _transactionTraceStore;
        
        public TransactionTraceManager(TransactionTraceStore transactionTraceStore)
        {
            _transactionTraceStore = transactionTraceStore;
        }

        private string GetDisambiguatedKey(Hash txId, Hash disambiguationHash)
        {
            var hash = disambiguationHash == null ? txId : Hash.Xor(disambiguationHash, txId);
            return hash.ToHex();
        }
        
        public async Task AddTransactionTraceAsync(TransactionTrace tr, Hash disambiguationHash = null)
        {
            var key = GetDisambiguatedKey(tr.TransactionId, disambiguationHash);
            await _transactionTraceStore.SetAsync(key, tr);
        }

        public async Task<TransactionTrace> GetTransactionTraceAsync(Hash txId, Hash disambiguationHash = null)
        {
            var key = GetDisambiguatedKey(txId, disambiguationHash);
            return await _transactionTraceStore.GetAsync<TransactionTrace>(key);
        }
    }
}