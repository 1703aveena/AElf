﻿using System.Threading.Tasks;

// ReSharper disable once CheckNamespace
namespace AElf.Node.Consensus
{
    public interface IConsensus
    {
        void Start(bool willingToMine);
        void DisposeConsensusEventList();
        Task UpdateConsensusInformation();
        bool IsAlive();
        bool Shutdown();
    }    
}