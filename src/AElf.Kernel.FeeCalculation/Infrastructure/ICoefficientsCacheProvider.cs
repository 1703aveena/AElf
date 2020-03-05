using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf.Contracts.MultiToken;
using AElf.Kernel.SmartContract.Application;
using Volo.Abp.DependencyInjection;

namespace AElf.Kernel.FeeCalculation.Infrastructure
{
    public interface ICoefficientsCacheProvider : ISyncCacheService
    {
        Task<IList<int[]>> GetCoefficientByTokenTypeAsync(int tokenType, IChainContext chainContext);
        void SetModifyHeight(int height);
    }

    public class CoefficientsCacheProvider : ICoefficientsCacheProvider, ISyncCacheProvider, ISingletonDependency
    {
        private readonly IBlockchainStateService _blockChainStateService;
        private readonly Dictionary<int, IList<int[]>> _coefficientsDicCache;
        private int _modifyHeight;

        public CoefficientsCacheProvider(IBlockchainStateService blockChainStateService)
        {
            _blockChainStateService = blockChainStateService;
            _coefficientsDicCache = new Dictionary<int, IList<int[]>>();
            _modifyHeight = 0;
        }

        public async Task<IList<int[]>> GetCoefficientByTokenTypeAsync(int tokenType, IChainContext chainContext)
        {
            if (_modifyHeight == 0)
            {
                if (_coefficientsDicCache.TryGetValue(tokenType, out var coefficientsInCache))
                    return coefficientsInCache;
                coefficientsInCache = await GetFromBlockChainStateAsync(tokenType, chainContext);
                _coefficientsDicCache[tokenType] = coefficientsInCache;
                return coefficientsInCache;
            }

            return await GetFromBlockChainStateAsync(tokenType, chainContext);
        }

        public void SetModifyHeight(int height)
        {
            _modifyHeight = height;
        }

        public async Task SyncCacheAsync(IChainContext chainContext)
        {
            var currentLibHeight = chainContext.BlockHeight;
            AllCalculateFeeCoefficients coefficientOfAllTokenType = null;
            if (_modifyHeight <= currentLibHeight)
            {
                var tokenTypeList = _coefficientsDicCache.Select(x => x.Key).ToArray();
                foreach (var tokenType in tokenTypeList)
                {
                    if (coefficientOfAllTokenType == null)
                        coefficientOfAllTokenType =
                            await _blockChainStateService.GetBlockExecutedDataAsync<AllCalculateFeeCoefficients>(
                                chainContext);
                    var targetTokeData =
                        coefficientOfAllTokenType.Value.FirstOrDefault(x => x.FeeTokenType == tokenType);
                    _coefficientsDicCache[tokenType] = targetTokeData.PieceCoefficientsList.AsEnumerable()
                        .Select(x => (int[]) (x.Value.AsEnumerable())).ToList();
                }
            }
            
            _modifyHeight = 0;
        }

        private async Task<IList<int[]>> GetFromBlockChainStateAsync(int tokenType, IChainContext chainContext)
        {
            var coefficientOfAllTokenType =
                await _blockChainStateService.GetBlockExecutedDataAsync<AllCalculateFeeCoefficients>(
                    chainContext);
            var targetTokeData =
                coefficientOfAllTokenType.Value.FirstOrDefault(x => x.FeeTokenType == tokenType);
            var coefficientsArray = targetTokeData.PieceCoefficientsList.AsEnumerable()
                .Select(x => (int[]) (x.Value.AsEnumerable())).ToList();
            return coefficientsArray;
        }
    }
}