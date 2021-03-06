using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf.Kernel.Blockchain.Application;
using AElf.Kernel.SmartContract.Domain;
using AElf.Types;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Threading;

namespace AElf.Kernel.SmartContract.Application
{
    public interface IBlockchainStateService
    {
        Task MergeBlockStateAsync(long lastIrreversibleBlockHeight, Hash lastIrreversibleBlockHash);

        Task SetBlockStateSetAsync(BlockStateSet blockStateSet);

        Task RemoveBlockStateSetsAsync(IList<Hash> blockStateHashes);
    }

    public interface IBlockchainExecutedDataService
    {
        Task<ByteString> GetBlockExecutedDataAsync(IBlockIndex chainContext, string key);

        Task AddBlockExecutedDataAsync(Hash blockHash, IDictionary<string, ByteString> blockExecutedData);
    }


    public class BlockchainExecutedDataService : IBlockchainExecutedDataService
    {
        private readonly IBlockchainExecutedDataManager _blockchainExecutedDataManager;

        //it's a infrastructure
        private ConcurrentDictionary<string, ByteString> _cache = new ConcurrentDictionary<string, ByteString>();

        public BlockchainExecutedDataService(IBlockchainExecutedDataManager blockchainExecutedDataManager)
        {
            _blockchainExecutedDataManager = blockchainExecutedDataManager;
        }


        public ILogger<BlockchainExecutedDataService> Logger { get; set; }


        public async Task<ByteString> GetBlockExecutedDataAsync(IBlockIndex chainContext, string key)
        {
            return (await _blockchainExecutedDataManager.GetExecutedCacheAsync(key, chainContext.BlockHeight,
                chainContext.BlockHash)).Value;
        }

        public async Task AddBlockExecutedDataAsync(Hash blockHash, IDictionary<string, ByteString> blockExecutedData)
        {
            await _blockchainExecutedDataManager.AddBlockExecutedCacheAsync(blockHash, blockExecutedData);
        }
    }

    public interface ICachedBlockchainExecutedDataService<T>
    {
        T GetBlockExecutedData(IBlockIndex chainContext, string key);
        Task AddBlockExecutedDataAsync(IBlockIndex blockIndex, IDictionary<string, T> blockExecutedData);
        void CleanChangeHeight(long height);
    }

    public class CachedBlockchainExecutedDataService<T> : ICachedBlockchainExecutedDataService<T>
    {
        private readonly IBlockchainExecutedDataManager _blockchainExecutedDataManager;

        //TODO: make a store in Infrastructure
        private readonly ConcurrentDictionary<string, T> _dictionary = new ConcurrentDictionary<string, T>();
        private readonly ConcurrentDictionary<string, long> _changeHeight = new ConcurrentDictionary<string, long>();

        public CachedBlockchainExecutedDataService(IBlockchainExecutedDataManager blockchainExecutedDataManager)
        {
            _blockchainExecutedDataManager = blockchainExecutedDataManager;
        }

        public T GetBlockExecutedData(IBlockIndex chainContext, string key)
        {
            if ( !_changeHeight.TryGetValue(key,out _) && _dictionary.TryGetValue(key, out var value))
            {
                return value;
            }

            var ret = AsyncHelper.RunSync(() => _blockchainExecutedDataManager.GetExecutedCacheAsync(key,
                chainContext.BlockHeight,
                chainContext.BlockHash));

            var blockExecutedData = Deserialize(ret.Value);
            
            //if executed is in Store, it will not change when forking
            if(ret.IsInStore && !_changeHeight.TryGetValue(key, out _))
                _dictionary[key] = blockExecutedData;
            return blockExecutedData;
        }

        public async Task AddBlockExecutedDataAsync(IBlockIndex blockIndex, IDictionary<string, T> blockExecutedData)
        {
            await _blockchainExecutedDataManager.AddBlockExecutedCacheAsync(blockIndex.BlockHash, blockExecutedData.ToDictionary
                (pair => pair.Key, pair => Serialize(pair.Value)));
            foreach (var pair in blockExecutedData)
            {
                if (blockIndex.BlockHeight > AElfConstants.GenesisBlockHeight &&
                    (!_changeHeight.TryGetValue(pair.Key, out var height) || height < blockIndex.BlockHeight))
                    _changeHeight[pair.Key] = blockIndex.BlockHeight;
                _dictionary.TryRemove(pair.Key, out _);
            }
        }

        public void CleanChangeHeight(long height)
        {
            
            var keys = new List<string>();
            foreach (var pair in _changeHeight)
            {
                if(pair.Value <= height) keys.Add(pair.Key);
            }

            foreach (var key in keys)
            {
                _changeHeight.TryRemove(key, out _);
            }
        }

        protected virtual T Deserialize(ByteString byteString)
        {
            return SerializationHelper.Deserialize<T>(byteString?.ToByteArray());
        }

        protected virtual ByteString Serialize(T value)
        {
            return ByteString.CopyFrom(SerializationHelper.Serialize(value));
        }
    }

    public class BlockchainStateService : IBlockchainStateService
    {
        private readonly IBlockchainService _blockchainService;
        private readonly IBlockStateSetManger _blockStateSetManger;
        public ILogger<BlockchainStateService> Logger { get; set; }

        public BlockchainStateService(IBlockchainService blockchainService,
            IBlockStateSetManger blockStateSetManger)
        {
            _blockchainService = blockchainService;
            _blockStateSetManger = blockStateSetManger;
            Logger = NullLogger<BlockchainStateService>.Instance;
        }

        public async Task MergeBlockStateAsync(long lastIrreversibleBlockHeight, Hash lastIrreversibleBlockHash)
        {
            var chainStateInfo = await _blockStateSetManger.GetChainStateInfoAsync();
            var firstHeightToMerge = chainStateInfo.BlockHeight == 0L
                ? AElfConstants.GenesisBlockHeight
                : chainStateInfo.BlockHeight + 1;
            var mergeCount = lastIrreversibleBlockHeight - firstHeightToMerge;
            if (mergeCount < 0)
            {
                Logger.LogWarning(
                    $"Last merge height: {chainStateInfo.BlockHeight}, lib height: {lastIrreversibleBlockHeight}, needn't merge");
                return;
            }

            var blockIndexes = new List<IBlockIndex>();
            if (chainStateInfo.Status == ChainStateMergingStatus.Merged)
            {
                blockIndexes.Add(new BlockIndex(chainStateInfo.MergingBlockHash, -1));
            }

            var reversedBlockIndexes =
                await _blockchainService.GetReversedBlockIndexes(lastIrreversibleBlockHash, (int) mergeCount);
            reversedBlockIndexes.Reverse();

            blockIndexes.AddRange(reversedBlockIndexes);

            blockIndexes.Add(new BlockIndex(lastIrreversibleBlockHash, lastIrreversibleBlockHeight));

            Logger.LogDebug(
                $"Start merge lib height: {lastIrreversibleBlockHeight}, lib block hash: {lastIrreversibleBlockHash}, merge count: {blockIndexes.Count}");

            foreach (var blockIndex in blockIndexes)
            {
                try
                {
                    Logger.LogTrace($"Merging state {chainStateInfo} for block {blockIndex}");
                    await _blockStateSetManger.MergeBlockStateAsync(chainStateInfo, blockIndex.BlockHash);
                }
                catch (Exception e)
                {
                    Logger.LogError(e,
                        $"Exception while merge state {chainStateInfo} for block {blockIndex}");
                    throw;
                }
            }
        }

        public async Task SetBlockStateSetAsync(BlockStateSet blockStateSet)
        {
            await _blockStateSetManger.SetBlockStateSetAsync(blockStateSet);
        }

        public async Task RemoveBlockStateSetsAsync(IList<Hash> blockStateHashes)
        {
            await _blockStateSetManger.RemoveBlockStateSetsAsync(blockStateHashes);
        }
    }
}