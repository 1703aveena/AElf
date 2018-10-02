﻿using System.Threading.Tasks;
using AElf.Cryptography.ECDSA;
using AElf.Kernel;
using AElf.Common;

namespace AElf.Miner.Miner
{
    public interface IMiner
    {
        void Init(ECKeyPair nodeKeyPair);
        void Close();

        Hash Coinbase { get; }
        
        /// <summary>
        /// mining functionality
        /// </summary>
        /// <returns></returns>
        Task<IBlock> Mine(Round currentRoundInfo = null);
    }
}