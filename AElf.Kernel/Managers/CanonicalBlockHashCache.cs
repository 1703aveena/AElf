﻿using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Net.Http.Headers;
using AElf.Kernel.EventMessages;
using Easy.MessageHub;

namespace AElf.Kernel.Managers
{
    public class CanonicalBlockHashCache
    {
        private ILightChain _lightChain;
        private int _filling;

        public ulong CurrentHeight { get; private set; }

        private readonly ConcurrentDictionary<ulong, Hash> _blocks = new ConcurrentDictionary<ulong, Hash>();

        public CanonicalBlockHashCache(ILightChain lightChain)
        {
            _lightChain = lightChain;
            MessageHub.Instance.Subscribe<BlockHeader>(
                async h => await OnNewBlockHeader(h));
            MessageHub.Instance.Subscribe<RevertedToBlockHeader>(
                async r => await OnNewBlockHeader(r.BlockHeader));
        }

        public Hash GetHashByHeight(ulong height)
        {
            if (_blocks.TryGetValue(height, out var hash)) return hash;

            if (_blocks.Count == 0)
            {
                RecoverCurrent().Wait();
            }

            _blocks.TryGetValue(height, out hash);
            return hash;
        }

        public async Task OnNewBlockHeader(BlockHeader header)
        {
            var height = header.Index;
            if (_blocks.Count == 0)
            {
                // If empty, just add
                AddToBlocks(height, header.GetHash());
            }
            else if (_blocks.TryGetValue(height - 1, out var prevHash) && prevHash == header.PreviousBlockHash)
            {
                // Current fork
                AddToBlocks(height, header.GetHash());
                if (height > Globals.ReferenceBlockValidPeriod)
                {
                    var toRemove = height - Globals.ReferenceBlockValidPeriod - 1;
                    _blocks.TryRemove(toRemove, out _);
                }
            }
            else
            {
                // Switch fork
                //_blocks.Clear();
                AddToBlocks(height, header.GetHash());
            }

            CurrentHeight = height;
            await MaybeFillBlocks();
        }

        private void AddToBlocks(ulong height, Hash blockHash)
        {
            if (!_blocks.ContainsKey(height))
            {
                _blocks.TryAdd(height, blockHash);
                return;
            }

            _blocks[height] = blockHash;
        }

        private async Task MaybeFillBlocks()
        {
            var height = CurrentHeight;
            if (Interlocked.CompareExchange(ref _filling, 1, 0) == 0)
            {
                for (var i = (ulong) 1; i <= Math.Max(Globals.ReferenceBlockValidPeriod, height); i++)
                {
                    if (height < i)
                    {
                        break;
                    }

                    if (_blocks.ContainsKey(height))
                    {
                        break;
                    }

                    await _lightChain.GetCanonicalHashAsync(height - i);
                }
            }
        }

        private async Task RecoverCurrent()
        {
            var curHeight = await _lightChain.GetCurrentBlockHeightAsync();
            var curHeader = await _lightChain.GetHeaderByHeightAsync(curHeight);
            await OnNewBlockHeader((BlockHeader) curHeader);
        }
    }
}