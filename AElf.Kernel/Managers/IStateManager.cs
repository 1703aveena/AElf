using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AElf.Common;
using AElf.Kernel.Storages;
using CSharpx;
using Volo.Abp.DependencyInjection;

namespace AElf.Kernel.Managers
{
    public interface IStateManager
    {
        Task SetAsync(StatePath path, byte[] value);

        Task<byte[]> GetAsync(StatePath path);

        Task PipelineSetAsync(Dictionary<StatePath, byte[]> pipelineSet);
    }

    public interface IBlockchainStateManager
    {
        //Task<VersionedState> GetVersionedStateAsync(Hash blockHash,long blockHeight, string key);
    }

    public class BlockchainStateManager : IBlockchainStateManager, ITransientDependency
    {
        private readonly IStateStore<VersionedState> _versionedStates;
        private readonly IStateStore<BlockStateSet> _blockStateSets;

        public BlockchainStateManager(IStateStore<VersionedState> versionedStates,
            IStateStore<BlockStateSet> blockStateSets)
        {
            _versionedStates = versionedStates;
            _blockStateSets = blockStateSets;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <param name="blockHeight"></param>
        /// <param name="blockHash">should already in store</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public async Task<VersionedState> GetStateAsync(string key, long blockHeight, Hash blockHash)
        {
            VersionedState value = null;

            //first DB read
            var bestChainState = await _versionedStates.GetAsync(key);

            if (bestChainState != null)
            {
                if (bestChainState.BlockHash == blockHash)
                {
                    value = bestChainState;
                }
                else
                {
                    if (bestChainState.BlockHeight > blockHeight)
                    {
                        //because we may clear history state
                        throw new ArgumentException("cannot read history state");
                    }
                    else
                    {
                        //find value in block state set
                        var blockStateKey = blockHash.ToHex();
                        var blockStateSet = await _blockStateSets.GetAsync(blockStateKey);
                        while (blockStateSet != null && blockStateSet.BlockHeight > bestChainState.BlockHeight)
                        {
                            if (blockStateSet.Changes.ContainsKey(key))
                            {
                                value = blockStateSet.Changes[key];
                                break;
                            }

                            blockStateKey = blockStateSet.PreviousHash?.ToHex();

                            if (blockStateKey != null)
                            {
                                blockStateSet = await _blockStateSets.GetAsync(blockStateKey);
                            }
                            else
                            {
                                blockStateSet = null;
                            }
                        }

                        if (value == null)
                        {
                            //not found value in block state sets. for example, best chain is 100, blockHeight is 105,
                            //it will find 105 ~ 101 block state set. so the value could only be the best chain state value.
                            value = bestChainState;
                        }
                    }
                }
            }
            else
            {
                //best chain state is null, it will find value in block state set
                var blockStateKey = blockHash.ToHex();
                var blockStateSet = await _blockStateSets.GetAsync(blockStateKey);
                while (blockStateSet != null)
                {
                    if (blockStateSet.Changes.ContainsKey(key))
                    {
                        value = blockStateSet.Changes[key];
                        break;
                    }

                    blockStateKey = blockStateSet.PreviousHash?.ToHex();

                    if (blockStateKey != null)
                    {
                        blockStateSet = await _blockStateSets.GetAsync(blockStateKey);
                    }
                    else
                    {
                        blockStateSet = null;
                    }
                }
            }

            return value;
        }

        public async Task SetBlockStateSetAsync(BlockStateSet blockStateSet)
        {
            await _blockStateSets.SetAsync(GetKey(blockStateSet), blockStateSet);
        }

        private string GetKey(BlockStateSet blockStateSet)
        {
            return blockStateSet.BlockHash.ToHex();
        }
    }
}