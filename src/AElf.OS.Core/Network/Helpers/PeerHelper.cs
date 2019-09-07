using System.Linq;
using AElf.OS.Network.Infrastructure;
using AElf.OS.Network.Types;

namespace AElf.OS.Network.Helpers
{
    public static class PeerHelper
    {
        public static PeerInfo FromNetworkPeer(IPeer peer)
        {
            return new PeerInfo
            {
                IpAddress = peer.RemoteEndpoint.ToString(),
                Pubkey = peer.Info.Pubkey,
                LastKnownLibHeight = peer.LastKnownLibHeight,
                ProtocolVersion = peer.Info.ProtocolVersion,
                ConnectionTime = peer.Info.ConnectionTime,
                Inbound = peer.Info.IsInbound,
                BufferedAnnouncementsCount = peer.BufferedAnnouncementsCount,
                BufferedBlocksCount = peer.BufferedBlocksCount,
                BufferedTransactionsCount = peer.BufferedTransactionsCount,
                RequestMetrics = peer.GetRequestMetrics()?.Values.SelectMany(kvp => kvp).ToList()
            };
        }
    }
}