﻿using System.Threading.Tasks;

// ReSharper disable once CheckNamespace
namespace AElf.Kernel.Node
{
    public interface IConsensus
    {
        Task Start();
        Task Update();
        Task RecoverMining();
    }
}