using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf.Common;
using AElf.Common.Extensions;
using AElf.Common.MultiIndexDictionary;
using AElf.Configuration;
using AElf.Kernel;
using Akka.Util.Internal;
using Easy.MessageHub;
using NLog;

// ReSharper disable once CheckNamespace
namespace AElf.ChainController
{
    public class BlockSet : IBlockSet
    {
        private readonly ILogger _logger;

        private readonly IndexedDictionary<IBlock> _dict;

        private object _ = new object();
        
        public BlockSet()
        {
            _logger = LogManager.GetLogger(nameof(BlockSet));

            _dict = new List<IBlock>()
                .IndexBy(b => b.Header.Index, true)
                .IndexBy(b => b.GetHash());
        }
        
        public void AddBlock(IBlock block)
        {
            var hash = block.GetHash().DumpHex();
            _logger?.Trace($"Added block {hash} to BlockSet.");
            lock (_)
            {
                _dict.Add(block);
            }
            
            // TODO: Need a way to organize branched chains (using indexes)
        }

        /// <summary>
        /// Tell the block collection the height of block just successfully executed.
        /// </summary>
        /// <param name="currentHeight"></param>
        /// <returns></returns>
        public void Tell(ulong currentHeight)
        {
            RemoveOldBlocks(currentHeight - (ulong) GlobalConfig.BlockNumberOfEachRound);
        }

        public bool IsBlockReceived(Hash blockHash, ulong height)
        {
            lock (_)
            {
                return _dict.Any(b => b.Header.Index == height && b.GetHash() == blockHash);
            }
        }

        public IBlock GetBlockByHash(Hash blockHash)
        {
            lock (_)
            {
                return _dict.FirstOrDefault(b => b.GetHash() == blockHash);
            }
        }

        public List<IBlock> GetBlockByHeight(ulong height)
        {
            lock (_)
            {
                return _dict.Where(b => b.Header.Index == height).ToList();
            }
        }

        private void RemoveOldBlocks(ulong targetHeight)
        {
            
        }
    }
}