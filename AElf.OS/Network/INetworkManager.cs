using System.Collections.Generic;
using System.Threading.Tasks;
using AElf.Common;
using AElf.Kernel;

namespace AElf.OS.Network
{
    public interface INetworkManager
    {
        Task<bool> AddPeer(string address);
        Task RemovePeer(string address);
        List<string> GetPeers();
        
        Task<IBlock> GetBlockByHash(Hash hash);
        
        Task Start();
        Task Stop();
    }
}