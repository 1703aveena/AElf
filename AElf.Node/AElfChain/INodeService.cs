﻿using System.Threading.Tasks;
using AElf.Kernel;

namespace AElf.Node.AElfChain
{
    // ReSharper disable InconsistentNaming
    public interface INodeService
    {
        void Initialize(NodeConfiguration conf);
        bool Start();
        bool Stop();
        Task<bool> CheckDPoSAliveAsync();
        Task<bool> CheckForkedAsync();

        Task<BlockHeaderList> GetBlockHeaderList(ulong index, int count);

        Task<Block> GetBlockFromHash(byte[] hash);
        Task<Block> GetBlockAtHeight(int height);
        Task<int> GetCurrentBlockHeightAsync();
    }
}