﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AElf.Kernel.Miner;
using AElf.Network.Peers;

namespace AElf.Kernel.Node.Protocol
{
    
    // Messages :
    
    
    // Initialization - On startup - For every peer get the height of the chain.
    // Distribute the work accordingly
    // We give everyone the current 


    class SyncPeer
    {
        Peer _peer;
    }
    
    public class BlockSynchedArgs : EventArgs
    {
        public Block Block { get; set; }
    }
    
    public class BlockSynchronizer
    {
        public event EventHandler BlockSynched;
        
        // React to new peers connected
        // when a peer connects get the height of his chain
        public IPeerManager _peerManager;
            
        private List<PendingBlock> _pendingBlocks;

        // The height of the chain 
        private int InitialHeight = 0;
        
        // The peers blockchain height with the highest height
        private int PeerHighestHight = 0;

        // The peer with the highest hight might not have all the blockchain
        // so the target height is PeerHighestHight + target.
        private int Security = 0;

        private Timer _cycleTimer;
        private IAElfNode _mainChainNode;

        public BlockSynchronizer(IAElfNode node)
        {
            _mainChainNode = node;
            _cycleTimer = new Timer(DoCycle, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
        }

        public void SetPeerHeight()
        {
            
        }

        private void DoCycle(object state)
        {
            
        }

        public void Start(Hash lastBlockHash)
        {
            // Start sync from block hash
        }

        /// <summary>
        /// When a block is received through the network it is placed here for sync
        /// purposes. Most of the time it will directly throw the <see cref="BlockSynched"/>
        /// event. In the case that the transaction was not received through the
        /// network, it will be placed here to sync.
        /// </summary>
        /// <param name="block"></param>
        public async Task AddBlockToSync(Block block)
        {
            List<Hash> missingTxs = _mainChainNode.GetMissingTransactions(block);

            // If no transactions are missing, directly fire the synced event.
            if (!missingTxs.Any())
            {
                BlockExecutionResult res = await _mainChainNode.AddBlock(block);
                
            }
            else
            {
                
            }
            
            // Called when the node receives a block
            // Get missing transactions from the pool
            // If no missing transactions 
            //     - Direct to pool
            // If some are missing
            //    - Add it to the sync pool
        }

        public void SetTransaction(byte[] blockHash, Transaction t)
        {
            //PendingBlock p = _pendingBlocks.Where()
        }
    }

    public class PendingBlock
    {
        private Block _block;
        private List<byte[]> _missingTxs = new List<byte[]>();

        public PendingBlock(Block block)
        {
            _block = block;
            BlockHash = block.GetHash();
        }

        public Hash BlockHash { get; }

        public void RemoveTransaction(byte[] txid)
        {
            
        }
    }
}