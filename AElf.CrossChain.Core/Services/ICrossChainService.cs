using System.Collections.Generic;
using System.Threading.Tasks;
using AElf.Common;

namespace AElf.CrossChain
{
    public interface ICrossChainService
    {
        Task<List<SideChainBlockData>> GetSideChainBlockDataAsync(Hash previousBlockHash,
            long preBlockHeight);
        Task<List<ParentChainBlockData>> GetParentChainBlockDataAsync(Hash previousBlockHash,
            long preBlockHeight);
        Task<bool> ValidateSideChainBlockDataAsync(IList<SideChainBlockData> sideChainBlockInfo,
            Hash previousBlockHash, long preBlockHeight);
        Task<bool> ValidateParentChainBlockDataAsync(IList<ParentChainBlockData> parentChainBlockInfo,
            Hash previousBlockHash, long preBlockHeight);

        void CreateNewSideChainBlockInfoCache();
    }
}