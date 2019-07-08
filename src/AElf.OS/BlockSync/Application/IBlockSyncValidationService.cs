using System.Threading.Tasks;
using AElf.Kernel;
using AElf.Types;

namespace AElf.OS.BlockSync.Application
{
    public interface IBlockSyncValidationService
    {
        Task<bool> ValidateBeforeHandleAnnounceAsync(Chain chain, Hash syncBlockHash, long syncBlockHeight);
    }
}