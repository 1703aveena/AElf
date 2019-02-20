using System.Collections.Generic;
using System.Threading.Tasks;
using AElf.Kernel.Services;

namespace AElf.Kernel.Blockchain.Application
{
    public class BlockExtraDataService : IBlockExtraDataService
    {
        private readonly IEnumerable<IBlockExtraDataProvider> _blockExtraDataProviders;

        public BlockExtraDataService(IEnumerable<IBlockExtraDataProvider> blockExtraDataProviders)
        {
            _blockExtraDataProviders = blockExtraDataProviders;
        }

        public async Task FillBlockExtraData(int chainId, Block block)
        {
            foreach (var blockExtraDataProvider in _blockExtraDataProviders)
            {
                await blockExtraDataProvider.FillExtraData(chainId, block);
            }
        }
    }
}