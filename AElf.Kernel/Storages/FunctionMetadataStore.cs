using AElf.Common;
using AElf.Common.Serializers;
using AElf.Database;

namespace AElf.Kernel.Storages
{
    public class FunctionMetadataStore : KeyValueStoreBase, IFunctionMetadataStore
    {
        public FunctionMetadataStore(IKeyValueDatabase keyValueDatabase, IByteSerializer byteSerializer)
            : base(keyValueDatabase, byteSerializer, GlobalConfig.MetadataPrefix)
        {
        }
    }
}
