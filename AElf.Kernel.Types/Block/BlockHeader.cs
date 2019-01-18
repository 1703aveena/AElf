﻿using AElf.Cryptography.ECDSA;
using AElf.Common;
using Google.Protobuf;

// ReSharper disable once CheckNamespace
namespace AElf.Kernel
{
    public partial class BlockHeader: IBlockHeader
    {
        private Hash _blockHash;
        
        public BlockHeader(Hash preBlockHash)
        {
            PreviousBlockHash = preBlockHash;
        }

        public Hash GetHash()
        {
            if (_blockHash == null)
            {
                _blockHash = Hash.FromRawBytes(GetSignatureData());
            }

            return _blockHash;
        }

        public byte[] GetHashBytes()
        {
            if (_blockHash == null)
                _blockHash = Hash.FromRawBytes(GetSignatureData());

            return _blockHash.DumpByteArray();
        }
        
        public ECSignature GetSignature()
        {
            return new ECSignature(Sig.ToByteArray());
        }

        private byte[] GetSignatureData()
        {
            var rawBlock = new BlockHeader
            {
                ChainId = ChainId,
                Index = Index,
                PreviousBlockHash = PreviousBlockHash?.Clone(),
                MerkleTreeRootOfTransactions = MerkleTreeRootOfTransactions?.Clone(),
                MerkleTreeRootOfWorldState = MerkleTreeRootOfWorldState?.Clone(),
                Bloom = Bloom,
                SideChainTransactionsRoot = MerkleTreeRootOfTransactions?.Clone()
            };
            if (Index > GlobalConfig.GenesisBlockHeight)
                rawBlock.Time = Time?.Clone();

            return rawBlock.ToByteArray();
        }

        public Hash GetDisambiguationHash()
        {
            return HashHelpers.GetDisambiguationHash(Index, Hash.FromRawBytes(P.ToByteArray()));
        }
    }
}