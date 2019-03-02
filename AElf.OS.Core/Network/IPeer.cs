using System.Collections.Generic;
using System.Threading.Tasks;
using AElf.Common;
using AElf.Kernel;

namespace AElf.OS.Network
{
    public interface IPeer
    {
        string PeerAddress { get; }
        
        Task SendDisconnectAsync();
        Task StopAsync();

        //TODO: change announce(height,blockHash)
        Task AnnounceAsync(BlockHeader header);
        Task SendTransactionAsync(Transaction tx);
        Task<Block> RequestBlockAsync(Hash hash, ulong height);
        Task<List<Hash>> GetBlockIdsAsync(Hash topHash, int count);
    }
}