﻿using System;
using AElf.Kernel.Types;
using Google.Protobuf.WellKnownTypes;
using AElf.Common;

namespace AElf.Kernel
{
    public class GenesisBlockBuilder
    {
        public Block Block { get; set; }

        public GenesisBlockBuilder Build(int chainId)
        {
            var block = new Block(Hash.Genesis)
            {
                Header = new BlockHeader
                {
                    Height = GlobalConfig.GenesisBlockHeight,
                    PreviousBlockHash = Hash.Genesis,
                    ChainId = chainId,
                    Time = Timestamp.FromDateTime(DateTime.UtcNow),
                    MerkleTreeRootOfWorldState = Hash.Default
                },
                Body = new BlockBody()
            };

            // Genesis block is empty
            // TODO: Maybe add info like Consensus protocol in Genesis block

            block.Header.MerkleTreeRootOfTransactions = block.Body.CalculateMerkleTreeRoots();
            block.Body.Complete(block.Header.GetHash());         
            Block = block;

            return this;
        }
    }
}