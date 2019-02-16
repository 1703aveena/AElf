using System;
using System.Linq;
using System.Threading.Tasks;
using AElf.Common;
using AElf.Kernel;
using AElf.Kernel.Services;
using AElf.OS.Network.Events;
using AElf.OS.Network.Grpc.Events;
using AElf.OS.Network.Temp;
using Google.Protobuf;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Volo.Abp.EventBus.Local;
using Volo.Abp.Threading;

namespace AElf.OS.Network.Grpc
{
    /// <summary>
    /// Implementation of the grpc generated service. It contains the rpc methods
    /// exposed to peers.
    /// </summary>
    public class GrpcServerService : PeerService.PeerServiceBase, IAElfServerService
    {
        private readonly ChainOptions _chainOptions;
        
        private readonly IPeerPool _peerPool;
        private readonly IFullBlockchainService _blockChainService;
        
        public ILocalEventBus EventBus { get; set; }

        public ILogger<GrpcServerService> Logger;
        
        private int ChainId
        {
            get { return _chainOptions.ChainId.ConvertBase58ToChainId(); }
        }
        
        public GrpcServerService(IOptionsSnapshot<ChainOptions> options, IPeerPool peerPool, IFullBlockchainService blockChainService)
        {
            _peerPool = peerPool;
            _blockChainService = blockChainService;
            
            EventBus = NullLocalEventBus.Instance;
            Logger = NullLogger<GrpcServerService>.Instance;

            _chainOptions = options.Value;
        }

        /// <summary>
        /// First step of the connect/auth process. Used to initiate a connection. The provided payload should be the
        /// clients authentication information. When receiving this call, protocol dictates you send the client your auth
        /// information. The response says whether or not you can connect.
        /// </summary>
        public override Task<AuthResponse> Connect(Handshake handshake, ServerCallContext context)
        {
            Logger?.LogTrace($"[{context.Peer}] has initiated a connection request.");
            
            try
            {                
                var peer = GrpcUrl.Parse(context.Peer);
                var peerServer = peer.IpAddress + ":" + handshake.HskData.ListeningPort;
                
                Logger?.LogDebug($"Attempting to create channel to {peerServer}");
                
                Channel channel = new Channel(peerServer, ChannelCredentials.Insecure);
                var client = new PeerService.PeerServiceClient(channel);

                if (channel.State != ChannelState.Ready)
                {
                    var c = channel.WaitForStateChangedAsync(channel.State);
                }

                var grpcPeer = new GrpcPeer(channel, client, handshake.HskData, peerServer, peer.Port.ToString());
                
                // Verify auth
                bool valid = _peerPool.AuthenticatePeer(peerServer, grpcPeer.PublicKey, handshake);

                if (!valid)
                    return Task.FromResult(new AuthResponse { Err = AuthError.WrongAuth });
                
                // send our credentials
                var hsk = AsyncHelper.RunSync(_peerPool.GetHandshakeAsync);
                var resp = client.Authentify(hsk);
                
                // If auth ok -> add it to our peers
                _peerPool.AddPeer(grpcPeer);
                
                return Task.FromResult(new AuthResponse { Success = true, Port = resp.Port });
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Error during connect.");
                return Task.FromResult(new AuthResponse { Err = AuthError.UnknownError });
            }
        }

        /// <summary>
        /// Second step of the connect/auth process. This takes place after the connect to receive the peers
        /// information and on return let him know that we've validated.
        /// </summary>
        public override Task<AuthResponse> Authentify(Handshake request, ServerCallContext context)
        {
            var peer = GrpcUrl.Parse(context.Peer);
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
                    block = AsyncHelper.RunSync(() => _blockChainService.GetBlockByHashAsync(ChainId, Hash.LoadByteArray(request.Id.ToByteArray())));
                }
                else
                {
                    block = AsyncHelper.RunSync(() => _blockChainService.GetBlockByHeightAsync(ChainId, (ulong)request.BlockNumber));
                }
                
                return Task.FromResult(new BlockReply { Block = block });
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Error during RequestBlock handle.");
            }
            
            return Task.FromResult(new BlockReply());
        }

        public override Task<BlockIdList> RequestBlockIds(BlockIdsRequest request, ServerCallContext context)
        {
            try
            {
                var headers = AsyncHelper.RunSync(() => _blockChainService.GetBlockHeaders(ChainId, Hash.LoadByteArray(request.FirstBlockId.ToByteArray()),
                    request.Count));
                
                BlockIdList list = new BlockIdList();
                list.Ids.AddRange(headers.Select(h => ByteString.CopyFrom(h.GetHash().DumpByteArray())).ToList());

                return Task.FromResult(list);
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Error during RequestBlock handle.");
            }
            
            return Task.FromResult(new BlockIdList());
        }

        /// <summary>
        /// Clients should call this method to disconnect explicitly.
        /// </summary>
        public override Task<VoidReply> Disconnect(DisconnectReason request, ServerCallContext context)
        {
            try
            {
                var peer = GrpcUrl.Parse(context.Peer);
                _peerPool.ProcessDisconnection(peer.Port.ToString());
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Error during Disconnect handle.");
            }
            
            return Task.FromResult(new VoidReply());
        }
    }
}