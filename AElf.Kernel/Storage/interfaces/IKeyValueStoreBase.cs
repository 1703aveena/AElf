using System.Collections.Generic;
using System.Threading.Tasks;

namespace AElf.Kernel.Storage.Interfaces
{
    public interface IKeyValueStoreBase
    {
        Task SetAsync(string key, object value);
        Task<bool> PipelineSetAsync(Dictionary<string, object> pipelineSet);
        Task<T> GetAsync<T>(string key);
        Task RemoveAsync(string key);
    }
}