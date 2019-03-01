﻿﻿using System;
 using System.Threading.Tasks;
 using AElf.Kernel;
using AElf.Common;
 using AElf.Kernel.Blockchain.Application;

 namespace AElf.Kernel.SmartContract
{
    public class TransactionContext : ITransactionContext
    {
        public TransactionContext()
        {
            Origin = new Address();
            Miner = new Address();
            PreviousBlockHash = new Hash();
            Transaction = new Transaction();
            Trace = new TransactionTrace();
            BlockHeight = 0;
            CallDepth = 0;
        }
        public Address Origin { get; set; }
        public Address Miner { get; set; }
        public Hash PreviousBlockHash { get; set; }
        public ulong BlockHeight { get; set; }
        public DateTime CurrentBlockTime { get; set; }
        public int CallDepth { get; set; }
        public Transaction Transaction { get; set; }
        public TransactionTrace Trace { get; set; }
        
        public IBlockchainService BlockchainService { get; set; }
        public Task<Block> GetBlockByHashAsync(int chainId, Hash blockId)
        {
            return BlockchainService.GetBlockByHashAsync(chainId,blockId);
        }
    }
}
