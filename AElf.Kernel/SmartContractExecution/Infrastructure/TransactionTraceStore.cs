using AElf.Common;
using AElf.Common.Serializers;
using AElf.Database;

namespace AElf.Kernel.Storages
{
    public class TransactionTraceStore : KeyValueStoreBase<StateKeyValueDbContext>, ITransactionTraceStore
    {
        public TransactionTraceStore(IByteSerializer byteSerializer, StateKeyValueDbContext keyValueDbContext) 
            : base(byteSerializer, keyValueDbContext, GlobalConfig.TransactionTracePrefix)
        {
        }
    }
}
