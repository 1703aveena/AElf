using AElf.Common;
using AElf.Common.Serializers;
using AElf.Database;

namespace AElf.Kernel.Storages
{
    public class CanonicalStore : KeyValueStoreBase<BlockChainKeyValueDbContext>, ICanonicalStore
    {
        public CanonicalStore(IByteSerializer byteSerializer, BlockChainKeyValueDbContext keyValueDbContext, string dataPrefix) 
            : base(byteSerializer, keyValueDbContext, GlobalConfig.CanonicalPrefix)
        {
        }
    }
}
