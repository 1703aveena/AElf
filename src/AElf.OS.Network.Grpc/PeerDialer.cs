using System;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Extensions.Options;

namespace AElf.OS.Network.Grpc
{
    /// <summary>
    /// Provides functionality to setup a connection to a distant node by exchanging some
    /// low level information.
    /// </summary>
    public class PeerDialer : IPeerDialer
    {
        private NetworkOptions NetworkOptions => NetworkOptionsSnapshot.Value;
        public IOptionsSnapshot<NetworkOptions> NetworkOptionsSnapshot { get; set; }
        
        private readonly IPeerClientFactory _peerClientFactory;

        public PeerDialer(IPeerClientFactory peerClientFactory)
        {
            _peerClientFactory = peerClientFactory;
        }

        /// <summary>
        /// Given an IP address, will create a connection to the distant node for
        /// further communications.
        /// </summary>
        /// <returns>The created peer</returns>
        public async Task<GrpcPeer> DialPeerAsync(string ipAddress, ConnectionInfo connectionInfo)
        {
            var (channel, client) = _peerClientFactory.CreateClientAsync(ipAddress);
            
            ConnectReply connectReply = await CallConnectAsync(client, channel, ipAddress, connectionInfo);

            if (connectReply?.Info?.Pubkey == null || connectReply.Error != ConnectError.ConnectOk)
            {
                throw await ExceptionHelpers.CleanupAndGetExceptionAsync($"Connect error: {connectReply?.Error}.", channel);
            }

            return new GrpcPeer(channel, client, ipAddress, connectReply.Info.ToPeerInfo(false));
        }

        /// <summary>
        /// Calls the server side connect RPC method, in order to establish a 2-way connection.
        /// </summary>
        /// <returns>The reply from the server.</returns>
        private async Task<ConnectReply> CallConnectAsync(PeerService.PeerServiceClient client, Channel channel, 
            string ipAddress, ConnectionInfo connectionInfo)
        {
            ConnectReply connectReply;
            
            try
            {
                var metadata = new Metadata {
                    {GrpcConstants.TimeoutMetadataKey, (NetworkOptions.PeerDialTimeoutInMilliSeconds*2).ToString()}};
                
                connectReply = await client.ConnectAsync(new ConnectRequest { Info = connectionInfo }, metadata);
            }
            catch (AggregateException ex)
            {
                throw await ExceptionHelpers.CleanupAndGetExceptionAsync($"Could not connect to {ipAddress}.", channel, ex);
            }
            
            return connectReply;
        }
        
        public async Task<GrpcPeer> DialBackPeer(string ipAddress, ConnectionInfo connectionInfo)
        {
            var (channel, client) = _peerClientFactory.CreateClientAsync(ipAddress);

            await PingNodeAsync(client, channel, ipAddress);
            
            return new GrpcPeer(channel, client, ipAddress, connectionInfo.ToPeerInfo(isInbound: true));
        }
        
        /// <summary>
        /// Checks that the distant node is reachable by pinging it.
        /// </summary>
        /// <returns>The reply from the server.</returns>
        private async Task PingNodeAsync(PeerService.PeerServiceClient client, Channel channel, string ipAddress)
        {
            try
            {
                var metadata = new Metadata {
                    {GrpcConstants.TimeoutMetadataKey, NetworkOptions.PeerDialTimeoutInMilliSeconds.ToString()}};
                
                await client.PingAsync(new PingRequest(), metadata);
            }
            catch (AggregateException ex)
            {
                throw await ExceptionHelpers.CleanupAndGetExceptionAsync($"Could not ping {ipAddress}.", channel, ex);
            }
        }
    }
}