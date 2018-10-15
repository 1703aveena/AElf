using System.Threading.Tasks;
using AElf.Kernel;

// ReSharper disable once CheckNamespace
namespace AElf.ChainController
{
    // ReSharper disable InconsistentNaming
    public interface IBlockExecutionService
    {
        Task<BlockExecutionResultCC> ExecuteBlock(IBlock block);
        void Start();
    }
}