﻿using System.Threading.Tasks;
using AElf.ChainController;
using AElf.Common;
using AElf.Kernel.Managers;
using AElf.Miner.TxMemPool;

namespace AElf.Kernel.Node
{
    public class P2PHandler
    {
        public IChainService ChainService { get; set; }
        public IBlockSynchronizor BlockSynchronizor { get; set; }
        public ITxPool TxPool { get; set; }
        public ITransactionManager TransactionManager { get; set; }

        public async Task<Block> GetBlockAtHeight(int height)
        {
            //var blockchain = ChainService.GetBlockChain(Hash.LoadHex(NodeConfig.Instance.ChainId));
            //return (Block) await blockchain.GetBlockByHeightAsync((ulong) height);

            return (Block) await ChainService.GetBlockChain(Hash.Default).GetBlockByHeightAsync((ulong)height);
        }

        public async Task<Block> GetBlockFromHash(Hash hash)
        {
            return await Task.Run(() => (Block) BlockSynchronizor.GetBlockByHash(hash));
        }

        public async Task<Transaction> GetTransaction(Hash txId)
        {
            if (TxPool.TryGetTx(txId, out var tx))
            {
                return tx;
            }

            return await TransactionManager.GetTransaction(txId);
        }
    }
}