using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf.Common;
using AElf.CrossChain.Cache;
using AElf.Kernel.Account.Application;
using AElf.Kernel.Blockchain.Application;
using AElf.Kernel.Blockchain.Domain;
using AElf.Kernel.SmartContract.Application;
using AElf.TestBase;
using Moq;
using Volo.Abp;
using Volo.Abp.EventBus.Local;

namespace AElf.CrossChain
{
    public class CrossChainTestBase : AElfIntegratedTest<CrossChainTestModule>
    {
        protected ITransactionResultQueryService TransactionResultQueryService;
        protected ITransactionResultService TransactionResultService;

        public CrossChainTestBase()
        {
            TransactionResultQueryService = GetRequiredService<ITransactionResultQueryService>();
            TransactionResultService = GetRequiredService<ITransactionResultService>();
        }

        protected IMultiChainBlockInfoCacheProvider CreateFakeMultiChainBlockInfoCacheProvider(
            Dictionary<int, List<IBlockInfo>> fakeCache)
        {
            var multiChainBlockInfoCacheProvider = new MultiChainBlockInfoCacheProvider();
            foreach (var (chainId, blockInfos) in fakeCache)
            {
                var blockInfoCache = new BlockInfoCache(1);
                multiChainBlockInfoCacheProvider.AddBlockInfoCache(chainId, blockInfoCache);
                foreach (var blockInfo in blockInfos)
                {
                    blockInfoCache.TryAdd(blockInfo);
                }
            }

            return multiChainBlockInfoCacheProvider;
        }

        protected ICrossChainDataConsumer CreateFakeCrossChainDataConsumer(
            IMultiChainBlockInfoCacheProvider multiChainBlockInfoCacheProvider)
        {
            return new CrossChainDataConsumer(multiChainBlockInfoCacheProvider);
        }

        protected ICrossChainContractReader CreateFakeCrossChainContractReader(
            Dictionary<int, ulong> sideChainIdHeights, Dictionary<int, ulong> parentChainIdHeights)
        {
            Mock<ICrossChainContractReader> mockObject = new Mock<ICrossChainContractReader>();
            mockObject.Setup(
                    m => m.GetSideChainCurrentHeightAsync(It.IsAny<int>(), It.IsAny<Hash>(), It.IsAny<ulong>()))
                .Returns<int, Hash, ulong>((sideChainId, preBlockHash, preBlockHeight) =>
                    Task.FromResult<ulong>(sideChainIdHeights.ContainsKey(sideChainId)
                        ? sideChainIdHeights[sideChainId]
                        : 0));
            mockObject.Setup(m => m.GetParentChainCurrentHeightAsync(It.IsAny<Hash>(), It.IsAny<ulong>()))
                .Returns(Task.FromResult<ulong>(parentChainIdHeights.Count > 0
                    ? parentChainIdHeights.Values.FirstOrDefault()
                    : 0));
            mockObject.Setup(m => m.GetSideChainIdAndHeightAsync(It.IsAny<Hash>(), It.IsAny<ulong>()))
                .Returns(Task.FromResult(new Dictionary<int, ulong>(sideChainIdHeights)));
            mockObject.Setup(m => m.GetAllChainsIdAndHeightAsync(It.IsAny<Hash>(), It.IsAny<ulong>()))
                .Returns(Task.FromResult(
                    new Dictionary<int, ulong>(
                        new Dictionary<int, ulong>(sideChainIdHeights).Concat(parentChainIdHeights))));
            mockObject.Setup(m => m.GetParentChainIdAsync(It.IsAny<Hash>(), It.IsAny<ulong>()))
                .Returns(Task.FromResult(parentChainIdHeights.Keys.FirstOrDefault()));
            return mockObject.Object;
        }
    }
}