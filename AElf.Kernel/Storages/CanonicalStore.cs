using AElf.Common;
using AElf.Common.Serializers;
using AElf.Database;

namespace AElf.Kernel.Storages
{
    public class CanonicalStore : KeyValueStoreBase, ICanonicalStore
    {
        public CanonicalStore(IKeyValueDatabase keyValueDatabase, IByteSerializer byteSerializer)
            : base(keyValueDatabase, byteSerializer, GlobalConfig.CanonicalPrefix)
        {
        }
    }
}
