﻿using System.Threading.Tasks;
using AElf.ChainController;
using AElf.Configuration;
using AElf.Kernel.Managers;

namespace AElf.Kernel.Node
{
    public class P2PHandler
    {
        public IChainService ChainService { get; set; }
        public INodeConfig NodeConfig { get; set; }
        public ITxPoolService TxPoolService { get; set; }
        public ITransactionManager TransactionManager { get; set; }

        public async Task<Block> GetBlockAtHeight(int height)
        {
            var blockchain = ChainService.GetBlockChain(NodeConfig.ChainId);
            return (Block) await blockchain.GetBlockByHeightAsync((ulong) height);
        }

        public async Task<ITransaction> GetTransaction(Hash txId)
        {
            if (TxPoolService.TryGetTx(txId, out var tx))
            {
                return tx;
            }

            return await TransactionManager.GetTransaction(txId);
        }
    }
}