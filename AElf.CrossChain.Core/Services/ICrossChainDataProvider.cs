using System.Collections.Generic;
using System.Threading.Tasks;
using AElf.Common;

namespace AElf.CrossChain
{
    public interface ICrossChainDataProvider
    {
        //TODO: return the list, not the boolean. do not change the parameters,
        //or it will be hard to read by other people
        Task<bool> GetSideChainBlockDataAsync(IList<SideChainBlockData> sideChainBlockData,
            Hash previousBlockHash, ulong preBlockHeight, bool isValidation = false);

        Task<bool> GetParentChainBlockDataAsync(IList<ParentChainBlockData> parentChainBlockData,
            Hash previousBlockHash, ulong preBlockHeight, bool isValidation = false);

        Task<bool> ActivateCrossChainCacheAsync(Hash blockHash, ulong blockHeight);

        void RegisterNewChain(int chainId);
        //void AddNewSideChainDataConsumer(ICrossChainDataConsumer crossChainDataConsumer);
        //int GetCachedChainCount();
        //void CreateNewSideChain();
    }
}