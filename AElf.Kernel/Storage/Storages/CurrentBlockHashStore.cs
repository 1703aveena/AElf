using AElf.Common;
using AElf.Common.Serializers;
using AElf.Database;
using AElf.Kernel.Storage.Interfaces;

namespace AElf.Kernel.Storage.Storages
{
    public class CurrentBlockHashStore : KeyValueStoreBase, ICurrentBlockHashStore
    {
        public CurrentBlockHashStore(IKeyValueDatabase keyValueDatabase, IByteSerializer byteSerializer)
            : base(keyValueDatabase, byteSerializer, GlobalConfig.CurrentBlockHashPrefix)
        {
        }
    }
}
