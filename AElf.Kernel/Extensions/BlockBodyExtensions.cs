using System.Collections.Generic;
using AElf.Common;

namespace AElf.Kernel.Extensions
{
    public static class BlockBodyExtensions
    {
        /// <summary>
        /// Set block header hash
        /// </summary>
        /// <param name="blockHeaderHash"></param>
        public static void Complete(this BlockBody blockBody, Hash blockHeaderHash)
        {
            blockBody.BlockHeader = blockHeaderHash;
        }

        /// <summary>
        /// Calculate merkle tree root of transaction and side chain block info. 
        /// </summary>
        /// <returns></returns>
        public static Hash CalculateMerkleTreeRoots(this BlockBody blockBody)
        {
            // side chain info
            if (blockBody.TransactionsCount == 0)
                return Hash.Default;
            if (blockBody.BinaryMerkleTree.Root != null)
                return blockBody.BinaryMerkleTree.Root;
            blockBody.BinaryMerkleTree.AddNodes(blockBody.Transactions);
            blockBody.BinaryMerkleTree.ComputeRootHash();

            return blockBody.BinaryMerkleTree.Root;
        }

        public static bool AddTransaction(this BlockBody blockBody, Transaction tx)
        {
            blockBody.Transactions.Add(tx.GetHash());
            blockBody.TransactionList.Add(tx);
            return true;
        }

        public static bool AddTransactions(this BlockBody blockBody, IEnumerable<Hash> txs)
        {
            blockBody.Transactions.Add(txs);
            return true;
        }
    }
}