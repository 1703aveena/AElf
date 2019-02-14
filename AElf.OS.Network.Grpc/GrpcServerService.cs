using System;
using System.Threading.Tasks;
using AElf.Common;
using AElf.Kernel;
using AElf.OS.Network.Events;
using AElf.OS.Network.Grpc.Events;
using AElf.OS.Network.Temp;
using Google.Protobuf;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Volo.Abp.EventBus.Local;

namespace AElf.OS.Network.Grpc
{
    /// <summary>
    /// Implementation of the grpc generated service. It contains the rpc methods
    /// exposed to peers.
    /// </summary>
    public class GrpcServerService : PeerService.PeerServiceBase, IAElfServerService
    {
        /// <summary>
        /// Event launched when a peer disconnects explicitly.
        /// </summary>
        public event EventHandler PeerSentDisconnection;
        
        private readonly IPeerManager _peerManager;
        private readonly IBlockService _blockService;
        
        public ILocalEventBus EventBus { get; set; }

        public ILogger<GrpcServerService> Logger;
        
        public GrpcServerService(IPeerManager peerManager, IBlockService blockService)
        {
            _peerManager = peerManager;
            _blockService = blockService;
            
            EventBus = NullLocalEventBus.Instance;
            Logger = NullLogger<GrpcServerService>.Instance;
        }

        /// <summary>
        /// First step of the connect/auth process.Used to initiate a connection. The provided payload should be the
        /// clients authentication information. When receiving this call, protocol dictates you send the client your auth
        /// information. The response says whether or not you can connect.
        /// </summary>
        public override Task<AuthResponse> Connect(Handshake request, ServerCallContext context)
        {
            Logger?.LogTrace($"[{context.Peer}] has initiated a connection request.");
            
            try
            {
                var peer = GrpcUrl.Parse(context.Peer);
                var peerServer = peer.IpAddress + ":" + request.HskData.ListeningPort;
                
                Logger?.LogDebug($"Attempting connect to {peerServer}");
                
                Channel channel = new Channel(peerServer, ChannelCredentials.Insecure);
                var client = new PeerService.PeerServiceClient(channel);

                if (channel.State != ChannelState.Ready)
                {
                    var c = channel.WaitForStateChangedAsync(channel.State);
                }

                bool isAuth = _peerManager.AuthenticatePeer(peerServer, request);
                
                // send our credentials
                var hsk = _peerManager.GetHandshake();
                var resp = client.Authentify(hsk);
                
                // If auth ok -> finalize
                _peerManager.FinalizeAuth(new GrpcPeer(channel, client, peerServer, peer.Port.ToString()));
                
                return Task.FromResult(new AuthResponse { Success = true, Port = resp.Port });
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Error during connect.");
                return Task.FromResult(new AuthResponse { Err = AuthError.UnknownError});
            }
        }

        /// <summary>
        /// Second step of the connect/auth process. This takes place after the connect to receive the peers
        /// information and on return let him know that we've validated.
        /// </summary>
        public override Task<AuthResponse> Authentify(Handshake request, ServerCallContext context)
        {
            Logger.LogTrace($"[{context.Peer}] Is calling back with his auth.");
            
            var peer = GrpcUrl.Parse(context.Peer);
            
            try
            {
                var peerServer = peer.IpAddress + ":" + request.HskData.ListeningPort;
                
                bool isAuth = _peerManager.AuthenticatePeer(peerServer, request);
                
                // todo verify auth
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Error during connect.");
                return Task.FromResult(new AuthResponse { Err = AuthError.UnknownError});
            }
            
            return Task.FromResult(new AuthResponse { Success = true, Port = peer.Port.ToString() });
        }

        /// <summary>
        /// This method is called when another peer broadcasts a transaction.
        /// </summary>
        public override Task<VoidReply> SendTransaction(Transaction tx, ServerCallContext context)
        {
            try
            {
                EventBus.PublishAsync(new TxReceivedEventData(tx));
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Error during connect.");
            }
            
            return Task.FromResult(new VoidReply());
        }

        /// <summary>
        /// This method is called when a peer wants to broadcast an announcement.
        /// </summary>
        public override Task<VoidReply> Announce(Announcement an, ServerCallContext context)
        {
            Logger.LogTrace($"Received announce {an.Id.ToByteArray().ToHex()} from {context.Peer}.");
            
            try
            {
                EventBus.PublishAsync(new AnnoucementReceivedEventData(Hash.LoadByteArray(an.Id.ToByteArray())));
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Error during announcement handle.");
            }
            
            return Task.FromResult(new VoidReply());
        }

        /// <summary>
        /// This method returns a block. The parameter is a <see cref="BlockRequest"/> object, if the value
        /// of <see cref="BlockRequest.Id"/> is not null, the request is by ID, otherwise it will be
        /// by height.
        /// </summary>
        public override Task<BlockReply> RequestBlock(BlockRequest request, ServerCallContext context)
        {
            if (request == null)
                return Task.FromResult(new BlockReply());
            
            try
            {
                Block block;
                if (request.Id != null && request.Id.Length > 0)
                {
                    block = _blockService.GetBlockAsync(Hash.LoadByteArray(request.Id.ToByteArray())).Result;
                }
                else
                {
                    block = _blockService.GetBlockByHeight((ulong)request.BlockNumber).Result;
                }
                
                return Task.FromResult(new BlockReply { Block = block });
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Error during RequestBlock handle.");
            }
            
            return Task.FromResult(new BlockReply());
        }

        /// <summary>
        /// Clients should call this method to disconnect explicitly.
        /// </summary>
        public override Task<VoidReply> Disconnect(DisconnectReason request, ServerCallContext context)
        {
            try
            {
                var peer = GrpcUrl.Parse(context.Peer);
                _peerManager.ProcessDisconnection(peer.Port.ToString());
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Error during Disconnect handle.");
            }
            
            return Task.FromResult(new VoidReply());
        }
    }
}