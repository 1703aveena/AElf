using System.Collections.Generic;
using System.Threading.Tasks;
using AElf.Common;
using AElf.Kernel.Blockchain.Domain;
using AElf.Kernel.Blockchain.Events;
using Volo.Abp.EventBus;
using Volo.Abp.EventBus.Local;

namespace AElf.CrossChain
{
    public class CrossChainService : ICrossChainService
    {
        private readonly ICrossChainDataProvider _crossChainDataProvider;
        private readonly IChainManager _chainManager;
        private ILocalEventBus LocalEventBus { get; }

        public CrossChainService(ICrossChainDataProvider crossChainDataProvider, IChainManager chainManager)
        {
            _crossChainDataProvider = crossChainDataProvider;
            _chainManager = chainManager;
            LocalEventBus = NullLocalEventBus.Instance;
            LocalEventBus.Subscribe<BestChainFoundEventData>(RegisterSideChainAsync);
        }

        public async Task<List<SideChainBlockData>> GetSideChainBlockDataAsync(Hash previousBlockHash,
            ulong preBlockHeight)
        {
            var res = new List<SideChainBlockData>();
            await _crossChainDataProvider.GetSideChainBlockDataAsync(res, previousBlockHash, preBlockHeight);
            return res;
        }

        public async Task<List<ParentChainBlockData>> GetParentChainBlockDataAsync(Hash previousBlockHash,
            ulong preBlockHeight)
        {
            var res = new List<ParentChainBlockData>();
            await _crossChainDataProvider.GetParentChainBlockDataAsync(res, previousBlockHash, preBlockHeight);
            return res;
        }

        public async Task<bool> ValidateSideChainBlockDataAsync(
            IList<SideChainBlockData> sideChainBlockData, Hash previousBlockHash, ulong preBlockHeight)
        {
            return await _crossChainDataProvider.GetSideChainBlockDataAsync(sideChainBlockData, 
                previousBlockHash, preBlockHeight, true);
        }
        
        public async Task<bool> ValidateParentChainBlockDataAsync(
            IList<ParentChainBlockData> parentChainBlockData, Hash previousBlockHash, ulong preBlockHeight)
        {
            return await _crossChainDataProvider.GetParentChainBlockDataAsync(parentChainBlockData, 
                previousBlockHash, preBlockHeight, true);
        }

        public void CreateNewSideChainBlockInfoCache()
        {
            _crossChainDataProvider.RegisterNewChain(_chainManager.GetChainId());
        }

        public async Task<CrossChainBlockData> GetIndexedCrossChainBlockDataAsync(Hash previousBlockHash, ulong previousBlockHeight)
        {
            return await _crossChainDataProvider.GetIndexedCrossChainBlockDataAsync(previousBlockHash,
                previousBlockHeight);
        }

        private async Task RegisterSideChainAsync(BestChainFoundEventData eventData)
        {
            await _crossChainDataProvider.ActivateCrossChainCacheAsync(eventData.BlockHash,
                eventData.BlockHeight);
        }
    }
}