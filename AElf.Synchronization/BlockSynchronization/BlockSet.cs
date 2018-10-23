using System;
using System.Collections.Generic;
using System.Linq;
using AElf.Common;
using AElf.Common.MultiIndexDictionary;
using AElf.Kernel;
using NLog;

// ReSharper disable once CheckNamespace
namespace AElf.Synchronization.BlockSynchronization
{
    public class BlockSet : IBlockSet
    {
        private readonly ILogger _logger;

        private readonly List<IBlock> _list;

        // ReSharper disable once FieldCanBeMadeReadOnly.Local
        private object _ = new object();

        private const ulong KeepHeight = 20;

        public BlockSet()
        {
            _logger = LogManager.GetLogger(nameof(BlockSet));

            _list = new List<IBlock>();
        }

        public void AddBlock(IBlock block)
        {
            var hash = block.GetHash().DumpHex();
            _logger?.Trace($"Added block {hash} to BlockSet.");
            lock (_)
            {
                _list.Add(block);
            }
        }

        /// <summary>
        /// Tell the block collection the height of block just successfully executed.
        /// </summary>
        /// <param name="currentHeight"></param>
        /// <returns></returns>
        public void Tell(ulong currentHeight)
        {
            if (currentHeight <= KeepHeight)
            {
                return;
            }
            
            RemoveOldBlocks(currentHeight - KeepHeight);
        }

        public bool IsBlockReceived(Hash blockHash, ulong height)
        {
            lock (_)
            {
                return _list.Any(b => b.Index == height && b.BlockHashToHex == blockHash.DumpHex());
            }
        }

        public IBlock GetBlockByHash(Hash blockHash)
        {
            lock (_)
            {
                return _list.FirstOrDefault(b => b.BlockHashToHex == blockHash.DumpHex());
            }
        }

        public List<IBlock> GetBlockByHeight(ulong height)
        {
            lock (_)
            {
                return _list.Where(b => b.Index == height).ToList();
            }
        }

        private void RemoveOldBlocks(ulong targetHeight)
        {
            try
            {
                lock (_)
                {
                    var toRemove = _list.Where(b => b.Index <= targetHeight).ToList();
                    if (!toRemove.Any())
                        return;
                    foreach (var block in toRemove)
                    {
                        _list.Remove(block);
                    }
                }
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }
        }

        /// <summary>
        /// Return the fork height if exists longer chain.
        /// </summary>
        /// <param name="currentHeight"></param>
        /// <returns></returns>
        public ulong AnyLongerValidChain(ulong currentHeight)
        {
            IEnumerable<IBlock> higherBlocks;
            ulong forkHeight = 0;
            
            lock (_)
            {
                higherBlocks = _list.Where(b => b.Index > currentHeight).ToList();
            }

            if (higherBlocks.Any())
            {
                _logger?.Trace("Find higher blocks in block set, will check whether there are longer valid chain.");
                foreach (var block in higherBlocks)
                {
                    var index = block.Index;
                    var flag = true;
                    while (flag)
                    {
                        lock (_)
                        {
                            if (_list.Any(b => b.Index == index - 1 && b.BlockHashToHex == block.BlockHashToHex))
                            {
                                index--;
                                forkHeight = index;
                            }
                            else
                            {
                                flag = false;
                            }
                        }
                    }
                }
            }
            
            return forkHeight <= currentHeight ? forkHeight : 0;
        }
    }
}