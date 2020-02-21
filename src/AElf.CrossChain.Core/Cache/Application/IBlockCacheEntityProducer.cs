using System;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace AElf.CrossChain.Cache.Application
{
    public interface IBlockCacheEntityProducer
    {
        bool TryAddBlockCacheEntity(IBlockCacheEntity blockCacheEntity);
    }
    
    public class BlockCacheEntityProducer : IBlockCacheEntityProducer, ISingletonDependency
    {
        private readonly ICrossChainCacheEntityProvider _crossChainCacheEntityProvider;

        public ILogger<BlockCacheEntityProducer> Logger { get; set; }
        
        public BlockCacheEntityProducer(ICrossChainCacheEntityProvider crossChainCacheEntityProvider)
        {
            _crossChainCacheEntityProvider = crossChainCacheEntityProvider;
        }

        public bool TryAddBlockCacheEntity(IBlockCacheEntity blockCacheEntity)
        {
            if (blockCacheEntity == null)
                throw new ArgumentNullException(nameof(blockCacheEntity));
            _crossChainCacheEntityProvider.TryGetChainCacheEntity(blockCacheEntity.ChainId, out var chainCacheEntity);

            var res = chainCacheEntity?.TryAdd(blockCacheEntity);
            if (res == null) return false;

            Logger.LogTrace(
                $"Cached height {blockCacheEntity.Height} from chain {ChainHelper.ConvertChainIdToBase58(blockCacheEntity.ChainId)}, {res}");
            return res.Value;
        }
    }
}