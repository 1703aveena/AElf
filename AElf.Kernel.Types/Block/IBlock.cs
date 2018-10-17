﻿using System.Collections.Generic;
using AElf.Cryptography.ECDSA;
using AElf.Common;

namespace AElf.Kernel
{
    public interface IBlock : IHashProvider, ISerializable
    {
        byte[] GetHashBytes();
        bool AddTransaction(Transaction tx);
        BlockHeader Header { get; set; }
        BlockBody Body { get; set; }
        void FillTxsMerkleTreeRootInHeader();
        Block Complete();
        bool AddTransactions(IEnumerable<Transaction> txHashes);
        void Sign(ECKeyPair keyPair);
    }
}