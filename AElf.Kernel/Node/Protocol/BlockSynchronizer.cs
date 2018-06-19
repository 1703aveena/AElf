﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using AElf.Common.ByteArrayHelpers;
using AElf.Kernel.Miner;
using AElf.Kernel.Node.Protocol.Exceptions;
using AElf.Network.Peers;

[assembly: InternalsVisibleTo("AElf.Kernel.Tests")]
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
            
        private List<PendingBlock> PendingBlocks { get; }

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
            PendingBlocks = new List<PendingBlock>();
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
            if (block?.Header == null || block.Body == null)
                throw new InvalidBlockException("The block, blockheader or body is null");
            
            if (block.Body.Transactions == null || block.Body.Transactions.Count <= 0)
                throw new InvalidBlockException("The block contains no transactions");

            byte[] h = null;
            try
            {
                h = block.GetHash().GetHashBytes();
            }
            catch (Exception e)
            {
                throw new InvalidBlockException("Invalid block hash");
            }

            if (GetBlock(h) != null)
                return;

            List<Hash> missingTxs = _mainChainNode.GetMissingTransactions(block);

            if (missingTxs.Any())
            {
                PendingBlock newPendingBlock = new PendingBlock(h, block);
                PendingBlocks.Add(newPendingBlock);
            }
            else
            {
                BlockExecutionResult res = await _mainChainNode.AddBlock(block);
            }
        }

        public void SetTransaction(byte[] blockHash, Transaction t)
        {
            //PendingBlock p = _pendingBlocks.Where()
        }

        public PendingBlock GetBlock(byte[] hash)
        {
            return PendingBlocks?.FirstOrDefault(p => p.BlockHash.BytesEqual(hash));
        }
    }

    public class PendingBlock
    {
        private Block _block;
        private List<byte[]> _missingTxs = new List<byte[]>();
        public byte[] BlockHash { get; }

        public PendingBlock(byte[] blockHash, Block block)
        {
            _block = block;
            BlockHash = blockHash;
        }
        
        public void RemoveTransaction(byte[] txid)
        {
            
        }
    }
}