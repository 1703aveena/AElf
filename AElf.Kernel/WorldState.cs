using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf.Kernel.Extensions;
using AElf.Kernel.KernelAccount;
using AElf.Kernel.Merkle;
using AElf.Kernel.Storages;

namespace AElf.Kernel
{
    public class WorldState : IWorldState
    {
        private readonly List<Change> _changes = new List<Change>();
        private readonly IChangesStore _changesStore;

        public WorldState(IChangesStore changesStore)
        {
            _changesStore = changesStore;
        }

        public Task ChangePointer(Path pointer, Hash blockHash)
        {
            var change = new Change
            {
                Before = pointer,
                After = pointer.SetBlockHash(blockHash)
            };
            _changes.Add(change);

            var path = pointer.SetBlockHashToNull();
            _changesStore.InsertAsync(path.GetPathHash(), change);
            
            return Task.CompletedTask;
        }
        
        public Task<Hash> GetWorldStateMerkleTreeRootAsync()
        {
            var pointerHashThatCanged = _changes.Select(ch => ch.Before.GetPointerHash());
            var merkleTree = new BinaryMerkleTree();
            merkleTree.AddNodes(pointerHashThatCanged);
            return Task.FromResult(merkleTree.ComputeRootHash());
        }
    }
}
