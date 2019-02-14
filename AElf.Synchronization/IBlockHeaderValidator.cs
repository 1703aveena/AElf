using System.Threading.Tasks;
using AElf.Kernel;

namespace AElf.Synchronization
{
    public interface IBlockHeaderValidator
    {
        Task<BlockHeaderValidationResult> ValidateBlockHeaderAsync(int chainId, BlockHeader blockHeader);
    }
}