using System.Threading.Tasks;
using AElf.Kernel;
using AElf.Kernel.Blockchain.Application;
using Volo.Abp.DependencyInjection;

namespace AElf.CrossChain
{
    public class LocalLibService : ILocalLibService, ITransientDependency
    {
        private readonly IBlockchainService _blockchainService;

        public LocalLibService(IBlockchainService blockchainService)
        {
            _blockchainService = blockchainService;
        }

        public async Task<Block> GetIrreversibleBlockByHeightAsync(long height)
        {
            var chain = await _blockchainService.GetChainAsync();
            if (chain.LastIrreversibleBlockHeight < height)
                return null;
            var blockHash = await _blockchainService.GetBlockHashByHeightAsync(chain, height);
            return await _blockchainService.GetBlockByHashAsync(blockHash);
        }
    }
}