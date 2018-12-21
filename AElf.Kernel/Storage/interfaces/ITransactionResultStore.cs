using System.Threading.Tasks;

namespace AElf.Kernel.Storage.Interfaces
{
    public interface ITransactionResultStore
    {
        Task SetAsync(string key, object value);
        Task<T> GetAsync<T>(string key);
    }
}