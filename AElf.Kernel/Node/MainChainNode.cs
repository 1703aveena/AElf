﻿using System;
using System.Threading.Tasks;
using AElf.Kernel.Managers;
using AElf.Kernel.Node.Network.Data;
using AElf.Kernel.Node.Network.Peers;
using AElf.Kernel.Node.RPC;
using AElf.Kernel.TxMemPool;
using Google.Protobuf;
using NLog;
using ServiceStack.Templates;

namespace AElf.Kernel.Node
{
    public class MainChainNode : IAElfNode
    {
        private readonly ITxPoolService _poolService;
        private readonly ITransactionManager _transactionManager;
        private readonly IRpcServer _rpcServer;
        private readonly ILogger _logger;
        private readonly IPeerManager _peerManager;
        
        public MainChainNode(ITxPoolService poolService, ITransactionManager txManager, IRpcServer rpcServer, 
            IPeerManager peerManager, ILogger logger)
        {
            _poolService = poolService;
            _transactionManager = txManager;
            _rpcServer = rpcServer;
            _peerManager = peerManager;
            _logger = logger;
        }

        public void Start()
        {
            _poolService.Start();
            _rpcServer.Start();
            
            _peerManager.Start();
            
            // todo : avoid circular dependency
            _rpcServer.SetCommandContext(this);
            _peerManager.SetCommandContext(this);
            
            _logger.Log(LogLevel.Debug, "AElf node started.");
        }

        public async Task<ITransaction> GetTransaction(Hash txId)
        {
            return await _transactionManager.GetTransaction(txId);
        }

        /// <summary>
        /// This inserts a transaction into the node. Note that it does
        /// not broadcast it to the network and doesn't add to the
        /// transaction pool. Essentially it just insert the transaction
        /// in the database.
        /// </summary>
        /// <param name="tx">The transaction to insert</param>
        /// <returns>The hash of the transaction that was inserted</returns>
        public async Task<IHash> InsertTransaction(Transaction tx)
        {
            return await _transactionManager.AddTransactionAsync(tx);
        }

        /// <summary>
        /// Broadcasts a transaction to the network. This method
        /// also places it in the transaction pool.
        /// </summary>
        /// <param name="tx">The tx to broadcast</param>
        public async Task BroadcastTransaction(Transaction tx)
        {
            // todo : send to network through server
            await _peerManager.BroadcastMessage(MessageTypes.BroadcastTx, tx.ToByteArray());
        }

        public async Task ReceiveTransaction(ByteString messagePayload)
        {
            try
            {
                Transaction tx = Transaction.Parser.ParseFrom(messagePayload);
                await _poolService.AddTxAsync(tx);
            }
            catch (Exception e)
            {
                _logger.Error(e, "Invalid tx - Could not receive transaction from the network", null);
            }
        }
    }
}