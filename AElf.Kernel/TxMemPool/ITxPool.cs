﻿using System.Collections.Generic;
using AElf.Kernel.TxMemPool;

namespace AElf.Kernel.TxMemPool
{
    public interface ITxPool
    {
        /// <summary>
        /// add tx
        /// </summary>
        /// <param name="tx"></param>
        /// <returns></returns>
        bool AddTx(Transaction tx);

        /// <summary>
        /// queue txs from tmp to waiting
        /// </summary>
        void QueueTxs();
        
        /// <summary>
        /// remove a tx
        /// </summary>
        /// <param name="txHash"></param>
        bool DisgardTx(Hash txHash);

        /// <summary>
        /// promote txs from waiting to executable
        /// </summary>
        /// <param name="addrs"></param>
        void Promote(List<Hash> addrs);
        
        /// <summary>
        /// return pool size
        /// </summary>
        /// <returns></returns>
        ulong Size { get; }
        
        /// <summary>
        /// return tx list can be executed
        /// </summary>
        List<Transaction> Ready { get; }

        /// <summary>
        /// threshold for entering pool
        /// </summary>
        ulong EntryThreshold { get; }
        
        /// <summary>
        /// minimal fee needed
        /// </summary>
        /// <returns></returns>
        Fee MinimalFee { get; }

        /// <summary>
        /// return a tx alread in pool
        /// </summary>
        /// <param name="txHash"></param>
        /// <param name="tx"></param>
        /// <returns></returns>
        bool GetTx(Hash txHash, out Transaction tx);

        /// <summary>
        /// return a tx alread in pool
        /// </summary>
        /// <param name="txHash"></param>
        /// <returns></returns>
        Transaction GetTx(Hash txHash);

        /// <summary>
        /// clear all txs in pool
        /// </summary>
        void ClearAll();

        /// <summary>
        /// clear all txs in waiting list
        /// </summary>
        void ClearWaiting();

        /// <summary>
        /// clear all txs in executable list
        /// </summary>
        void ClearExecutable();

        /// <summary>
        /// return true if contained in pool, otherwise false
        /// </summary>
        /// <param name="txHash"></param>
        bool Contains(Hash txHash);
        
        /// <summary>
        /// return waiting list size
        /// </summary>
        /// <returns></returns>
        ulong GetWaitingSize();
        
        /// <summary>
        /// return Executable list size
        /// </summary>
        /// <returns></returns>
        ulong GetExecutableSize();
        
        
        /// <summary>
        /// return Tmp list size
        /// </summary>
        /// <returns></returns>
        ulong GetTmpSize();
    }
}