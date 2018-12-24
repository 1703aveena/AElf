using AElf.Common;
using AElf.Common.Serializers;
using AElf.Database;

namespace AElf.Kernel.Storage
{
    public class CallGraphStore : KeyValueStoreBase
    {
        public CallGraphStore(IKeyValueDatabase keyValueDatabase, IByteSerializer byteSerializer)
            : base(keyValueDatabase, byteSerializer, GlobalConfig.CallGraphPrefix)
        {
        }
    }
}
