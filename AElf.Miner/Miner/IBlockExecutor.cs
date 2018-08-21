﻿using System.Threading.Tasks;
using AElf.Kernel;

namespace AElf.Miner.Miner
{
    public interface IBlockExecutor
    {
        Task<bool> ExecuteBlock(IBlock block);
        void Start();
    }
}