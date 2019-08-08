using System;
using System.Threading.Tasks;
using AElf.Cryptography;
using AElf.Kernel;
using AElf.OS.Network.Application;
using AElf.OS.Network.Events;
using AElf.OS.Network.Infrastructure;
using AElf.Types;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Volo.Abp.EventBus.Local;

namespace AElf.OS.Network.Grpc.Connection
{
    public class ConnectionService : IConnectionService
    {
        private ChainOptions ChainOptions => ChainOptionsSnapshot.Value;
        public IOptionsSnapshot<ChainOptions> ChainOptionsSnapshot { get; set; }
        private NetworkOptions NetworkOptions => NetworkOptionsSnapshot.Value;
        public IOptionsSnapshot<NetworkOptions> NetworkOptionsSnapshot { get; set; }
        
        private readonly IPeerPool _peerPool;
        private readonly IPeerDialer _peerDialer;
        private readonly IHandshakeProvider _handshakeProvider;
        public ILocalEventBus EventBus { get; set; }
        public ILogger<GrpcNetworkServer> Logger { get; set; }
        
        public ConnectionService(IPeerPool peerPool, IPeerDialer peerDialer, 
            IHandshakeProvider handshakeProvider)
        {
            _peerPool = peerPool;
            _peerDialer = peerDialer;
            _handshakeProvider = handshakeProvider;

            Logger = NullLogger<GrpcNetworkServer>.Instance;
            EventBus = NullLocalEventBus.Instance;
        }
        
        public async Task DisconnectAsync(IPeer peer, bool sendDisconnect = false)
        {
            if (peer == null)
                throw new ArgumentNullException(nameof(peer));
            
            // clean the pool
            if (_peerPool.RemovePeer(peer.Info.Pubkey) == null)
                Logger.LogWarning($"{peer} was not found in pool.");
            
            // clean the peer
            await peer.DisconnectAsync(sendDisconnect);
            
            Logger.LogDebug($"Removed peer {peer}");
        }

        public GrpcPeer GetPeerByPubkey(string pubkey)
        {
            return _peerPool.FindPeerByPublicKey(pubkey) as GrpcPeer;
        }

        /// <summary>
        /// Connects to a node with the given ip address and adds it to the node's peer pool.
        /// </summary>
        /// <param name="ipAddress">the ip address of the distant node</param>
        /// <returns>True if the connection was successful, false otherwise</returns>
        public async Task<bool> ConnectAsync(string ipAddress)
        {
            Logger.LogTrace($"Attempting to reach {ipAddress}.");

            if (_peerPool.FindPeerByAddress(ipAddress) != null)
            {
                Logger.LogWarning($"Peer {ipAddress} is already in the pool.");
                return false;
            }

            var peer = await _peerDialer.DialPeerAsync(ipAddress);

            if (peer == null)
                return false;

            if (NetworkOptions.AuthorizedPeers == AuthorizedPeers.Authorized &&
                !NetworkOptions.AuthorizedKeys.Contains(peer.Info.Pubkey))
            {
                Logger.LogDebug($"{peer.Info.Pubkey} not in the authorized peers.");
                return false;
            }

            if (!_peerPool.TryAddPeer(peer))
            {
                Logger.LogWarning($"Peer {peer.Info.Pubkey} is already in the pool.");
                await peer.DisconnectAsync(false);
                return false;
            }

            try
            {
                await peer.SendConfirmHandshakeAsync();
            }
            catch (Exception e)
            {
                await peer.DisconnectAsync(false);
                throw e;
            }
            peer.IsConnected = true;

            Logger.LogTrace($"Connected to {peer} - LIB height {peer.LastKnownLibHeight}, " +
                            $"best chain [{peer.CurrentBlockHeight}, {peer.CurrentBlockHash}].");

            FireConnectionEvent(peer);

            return true;
        }

        private void FireConnectionEvent(GrpcPeer peer)
        {
            var nodeInfo = new NodeInfo { Endpoint = peer.IpAddress, Pubkey = peer.Info.Pubkey.ToByteString() };
            var bestChainHash = peer.CurrentBlockHash;
            var bestChainHeight = peer.CurrentBlockHeight;

            _ = EventBus.PublishAsync(new PeerConnectedEventData(nodeInfo, bestChainHash, bestChainHeight));
        }
                
        public async Task<HandshakeReply> DoHandshakeAsync(string peerConnectionIp, Handshake handshake)
        {
            var peer = GrpcUrl.Parse(peerConnectionIp);

            if (peer == null)
                return new HandshakeReply();

            var pubkey = handshake.HandshakeData.Pubkey.ToHex();
            var currentPeer = _peerPool.FindPeerByPublicKey(pubkey);
            if (currentPeer != null)
            {
                Logger.LogWarning($"Cleaning up {currentPeer} already known.");
                return new HandshakeReply();
            }

            // TODO: find a URI type to use
            var peerAddress = peer.IpAddress + ":" + handshake.HandshakeData.ListeningPort;
            
            Logger.LogDebug($"Attempting to create channel to {peerAddress}");
            var grpcPeer = await _peerDialer.DialBackPeerAsync(peerAddress, handshake);
            
            if(grpcPeer == null)
                return new HandshakeReply();
            
            if (NetworkOptions.AuthorizedPeers == AuthorizedPeers.Authorized &&
                !NetworkOptions.AuthorizedKeys.Contains(grpcPeer.Info.Pubkey))
            {
                Logger.LogDebug($"{grpcPeer.Info.Pubkey} not in the authorized peers.");
                return new HandshakeReply();
            }

            // If auth ok -> add it to our peers
            if (!_peerPool.TryAddPeer(grpcPeer))
            {
                Logger.LogWarning($"Stopping connection, peer already in the pool {grpcPeer.Info.Pubkey}.");
                await grpcPeer.DisconnectAsync(false);
            }
            
            Logger.LogDebug($"Added to pool {grpcPeer.Info.Pubkey}.");

            var replyHandshake = await _handshakeProvider.GetHandshakeAsync();
            return new HandshakeReply {Handshake = replyHandshake};
        }

        public Task ConfirmHandshakeAsync(string peerPubkey)
        {
            var peer = _peerPool.FindPeerByPublicKey(peerPubkey) as GrpcPeer;
            if (peer == null)
            {
                Logger.LogWarning($"Cannot find Peer {peerPubkey} in the pool.");
                return Task.CompletedTask;
            }

            peer.IsConnected = true;
            return Task.CompletedTask;
        }

        private ConnectError ValidateConnectionInfo(ConnectionInfo connectionInfo)
        {
            // verify chain id
            if (connectionInfo.ChainId != ChainOptions.ChainId)
                return ConnectError.ChainMismatch;

            // verify protocol
            if (connectionInfo.Version != KernelConstants.ProtocolVersion)
                return ConnectError.ProtocolMismatch;
            
            // verify if we still have room for more peers
            if (NetworkOptions.MaxPeers != 0 && _peerPool.IsFull())
            {
                Logger.LogWarning($"Cannot add peer, there's currently {_peerPool.PeerCount} peers (max. {NetworkOptions.MaxPeers}).");
                return ConnectError.ConnectionRefused;
            }

            return ConnectError.ConnectOk;
        }

        private async Task<bool> ValidateHandshakeAsync(Handshake handshake)
        {
            if (!await _handshakeProvider.ValidateHandshakeAsync(handshake))
            {
                return false;
            }

            // verify authentication
            var pubkey = handshake.HandshakeData.Pubkey.ToHex();

            if (NetworkOptions.AuthorizedPeers == AuthorizedPeers.Authorized &&
                !NetworkOptions.AuthorizedKeys.Contains(pubkey))
            {
                Logger.LogDebug($"{pubkey} not in the authorized peers.");
                return false;
            }

            return true;
        }

        public async Task DisconnectPeersAsync(bool gracefulDisconnect)
        {
            var peers = _peerPool.GetPeers(true);
            foreach (var peer in peers)
            {
                await peer.DisconnectAsync(gracefulDisconnect);
            }
        }
        
        public void RemovePeer(string pubkey)
        {
            _peerPool.RemovePeer(pubkey);
        }
    }
}