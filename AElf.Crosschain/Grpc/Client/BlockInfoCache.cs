using System;
using System.Collections.Concurrent;
using System.Linq;
using AElf.Kernel;
using QuickGraph.Collections;

namespace AElf.Crosschain.Grpc.Client
{
    public class BlockInfoCache
    {
        private BlockingCollection<IBlockInfo> ToBeIndexedBlockInfoQueue { get;} =
            new BlockingCollection<IBlockInfo>(new ConcurrentQueue<IBlockInfo>());

        private Queue<IBlockInfo> CachedIndexedBlockInfoQueue { get;} = new Queue<IBlockInfo>();
        private readonly int _irreversible;
        private readonly int _cachedBoundedCapacity;
        public BlockInfoCache()
        {
            _irreversible = TypeConsts.MinimalBlockInfoCacheThreshold;
            _cachedBoundedCapacity = Math.Max(TypeConsts.MaximalCountForIndexingSideChainBlock,
                                         TypeConsts.MaximalCountForIndexingParentChainBlock) * _irreversible;
        }

        public bool TryAdd(IBlockInfo blockInfo)
        {
            return ToBeIndexedBlockInfoQueue.TryAdd(blockInfo);    
        }
        
        /// <summary>
        /// Try Take element from cached queue.
        /// </summary>
        /// <param name="height">Height of block info needed</param>
        /// <param name="blockInfo"></param>
        /// <param name="isCacheSizeLimited">Use <see cref="_cachedBoundedCapacity"/> as cache count threshold if true.</param>
        /// <returns></returns>
        public bool TryTake(ulong height, out IBlockInfo blockInfo, bool isCacheSizeLimited = false)
        {
            var first = First();
            // only mining process needs isCacheSizeLimited, for most nodes have this block.
            if (first != null 
                && first.Height == height && 
                !(isCacheSizeLimited && ToBeIndexedBlockInfoQueue.LastOrDefault()?.Height < height + (ulong) _irreversible))
            {
                var res = ToBeIndexedBlockInfoQueue.TryTake(out blockInfo, TypeConsts.WaitingIntervalInMillisecond);
                if(res)
                    CacheBlockInfo(blockInfo);
                
                return res;
            }
            
            // this is because of rollback 
            blockInfo = CachedIndexedBlockInfoQueue.FirstOrDefault(c => c.Height == height);
            if (blockInfo != null)
                return !isCacheSizeLimited ||
                       ToBeIndexedBlockInfoQueue.Count + CachedIndexedBlockInfoQueue.Count(ci => ci.Height >= height) >=
                       _cachedBoundedCapacity;
            
            return false;
        }
        
        /// <summary>
        /// Return first element in cached queue.
        /// </summary>
        /// <returns></returns>
        private IBlockInfo First()
        {
            return ToBeIndexedBlockInfoQueue.FirstOrDefault();
        }
        
        /// <summary>
        /// Cache block info lately removed.
        /// Dequeue one element if the cached count reaches <see cref="_cachedBoundedCapacity"/>
        /// </summary>                                                   
        /// <param name="blockInfo"></param>
        private void CacheBlockInfo(IBlockInfo blockInfo)
        {
            CachedIndexedBlockInfoQueue.Enqueue(blockInfo);
            if (CachedIndexedBlockInfoQueue.Count <= _cachedBoundedCapacity)
                return;
            CachedIndexedBlockInfoQueue.Dequeue();
        }
    }
}