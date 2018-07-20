﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using AElf.Network.Config;
using AElf.Network.Data;
using AElf.Network.Peers.Exceptions;
using Google.Protobuf;
using NLog;

[assembly:InternalsVisibleTo("AElf.Network.Tests")]
namespace AElf.Network.Peers
{
    public class PeerAddedEventArgs : EventArgs
    {
        public IPeer Peer { get; set; }
    }
    
    public class PeerRemovedEventArgs : EventArgs
    {
        public IPeer Peer { get; set; }
    }
    
    public class PeerManager : IPeerManager, IDisposable
    {
        public const int TargetPeerCount = 8; 
        
        public event EventHandler MessageReceived;
        public event EventHandler PeerListEmpty;

        public event EventHandler PeerAdded;
        public event EventHandler PeerRemoved;
        
        private readonly IAElfNetworkConfig _networkConfig;
        //private readonly INodeDialer _nodeDialer;
        //private readonly IAElfServer _server;
        private readonly IPeerDatabase _peerDatabase;
        private readonly ILogger _logger;

        // List of bootnodes that the manager was started with
        private readonly List<NodeData> _bootnodes = new List<NodeData>();
        
        // List of connected bootnodes
        private readonly List<IPeer> _bootnodePeers = new List<IPeer>();
        
        // List of non bootnode peers
        private readonly List<IPeer> _peers = new List<IPeer>();
        
        private readonly int _port;

        public bool UndergoingPm { get; private set; } = false;
        public bool ReceivingPeers { get; private set; } = false;

        //public int BootnodeDropThreshold = TargetPeerCount / 2;

        private Timer _maintenanceTimer = null;
        private readonly TimeSpan _initialMaintenanceDelay = TimeSpan.FromSeconds(5);
        private readonly TimeSpan _maintenancePeriod = TimeSpan.FromMinutes(1);
        
        public bool NoPeers { get; set; } = true;
        
        private ConnectionListner _connectionListener;

        public PeerManager(/*IAElfServer server,*/ IAElfNetworkConfig config, ILogger logger) //INodeDialer nodeDialer,
        {
            _connectionListener = new ConnectionListner(); // todo DI
            
            //_nodeDialer = nodeDialer;
            _networkConfig = config;
            _logger = logger;
            //_server = server;

            if (_networkConfig != null)
            {
                _port = config.Port;
                
                // Add the provided bootnodes
                if (_networkConfig.Bootnodes != null && _networkConfig.Bootnodes.Any())
                {
                    foreach (var node in _networkConfig.Bootnodes)
                    {
                        node.IsBootnode = true;
                        _bootnodes.Add(node);
                    }
                }
                else
                {
                    _logger?.Trace("Warning : bootnode list is empty.");
                }
            }
        }
        
//        private void HandleConnection(object sender, EventArgs e)
//        {
//            if (sender != null && e is ClientConnectedArgs args)
//            {
//                AddPeer(args.NewPeer);
//            }
//        }

        /// <summary>
        /// This method start the server that listens for incoming
        /// connections and sets up the manager.
        /// </summary>
        public void Start()
        {
            Task.Run(() => _connectionListener.StartListening(_port));
            _connectionListener.IncomingConnection += ConnectionListenerOnIncomingConnection;
            
            
            Setup().GetAwaiter().GetResult();
            
            //_server.ClientConnected += HandleConnection;
        }
        
        /// <summary>
        /// Sets up the server according to the configuration that was
        /// provided.
        /// </summary>
        private async Task Setup()
        {
            if (_networkConfig == null)
                return;
            
            if (_networkConfig.Peers.Any())
            {
                foreach (var peer in _networkConfig.Peers)
                {
                    NodeData nodeData = NodeData.FromString(peer);
                    await CreateAndAddPeer(nodeData);
                }
            }

            if (_peerDatabase != null)
            {
                var dbNodeData = _peerDatabase.ReadPeers();

                foreach (var p in dbNodeData)
                {
                    await CreateAndAddPeer(p);
                }
            }
            
            await AddBootnodes();

            if (_peers.Count < 1)
            {
                //throw new NoPeersConnectedException("Could not connect to any of the bootnodes");
                
                // Either a network problem or this node is the first to come online.
                NoPeers = true;
                PeerListEmpty?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                NoPeers = false;
            }

            _maintenanceTimer = new Timer(e => DoPeerMaintenance(), null, _initialMaintenanceDelay, _maintenancePeriod);
        }
        
        private void ConnectionListenerOnIncomingConnection(object sender, EventArgs eventArgs)
        {
            if (sender != null && eventArgs is IncomingConnectionArgs args)
            {
                CreatePeerFromConnection(args.Client);
            }
        }
        
        internal void DoPeerMaintenance()
        {
            List<IPeer> peersSnapshot = _peers.ToList();
            
            if (_peers == null)
                return;
            
            // If we're in the process of receiving peers (potentially modifiying _peers)
            // we return directly, we'll try again in the next cycle.
            if (ReceivingPeers)
                return;
            
            // If we're already in a maintenance cycle: do nothing
            if (UndergoingPm)
                return;
            
            UndergoingPm = true;

            // After either the initial maintenance operation or the removal operation
            // (mutually exclusive) adjust the peers to get to TargetPeerCount.
            try
            {
                int missingPeers = TargetPeerCount - _peers.Count;
                
                if (missingPeers > 0)
                {
                    // We set UndergoingPm here because at this point it will be ok for 
                    // us to receive peers 
                    UndergoingPm = false;
                    
                    var req = NetRequestFactory.CreateMissingPeersReq(missingPeers);
                    var taskAwaiter = BroadcastMessage(req);
                }
                else if (missingPeers < 0)
                {
                    // Here we will be modifying the _peers collection and we don't want
                    // anybody else modifying it.
                    
                    // Calculate peers to remove
                    List<IPeer> peersToRemove = GetPeersToRemove(Math.Abs(missingPeers));
                    
                    // Remove them
                    foreach (var peer in peersToRemove)
                       RemovePeer(peer);
                }
                else
                {
                    // Healthy peer list - nothing to do
                }
            }
            catch (Exception e)
            {
                ;
            }

            if (_peerDatabase != null)
            {
                if (_peers.Count >= peersSnapshot.Count)
                {
                    WritePeersToDb(_peers);
                }
            }

            UndergoingPm = false;
            
            if (_peers.Count < 1)
            {
                // Connection to all peers have been lost
                NoPeers = true;
                PeerListEmpty?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                NoPeers = false;
            }

        }

        /// <summary>
        /// Gets the peers to remove from the manager according to certain
        /// rules.
        /// todo : for now the rule is the first <see cref="count"/> peers
        /// </summary>
        /// <param name="count"></param>
        internal List<IPeer> GetPeersToRemove(int count)
        {
            // Calculate peers to remove
            List<IPeer> peersToRemove = _peers.Take(count).ToList();
            return peersToRemove;
        }

        internal async Task AddBootnodes()
        {
            foreach (var bootNode in _bootnodes)
            {
                await CreateAndAddPeer(bootNode);
            }
        }
        
        /// <summary>
        /// This method processes the peers received from one of
        /// the connected peers.
        /// </summary>
        /// <param name="messagePayload"></param>
        /// <returns></returns>
        internal async Task ReceivePeers(ByteString messagePayload)
        {
            // If we're in a maintenance cycle - do nothing
            // todo : maybe later we can queue this work...
            if (UndergoingPm)
                return;
                
            ReceivingPeers = true;
            
            try
            {
                PeerListData peerList = PeerListData.Parser.ParseFrom(messagePayload);
                
                if (peerList.NodeData.Count > 0)
                    _logger?.Trace("Peers received : " + peerList.GetLoggerString());

                foreach (var peer in peerList.NodeData)
                {
                    NodeData p = new NodeData
                    {
                        IpAddress = peer.IpAddress, 
                        Port = peer.Port,
                        IsBootnode = peer.IsBootnode
                    };
                    
                    IPeer newPeer = await CreateAndAddPeer(p);
                }
            }
            catch (Exception e)
            {
                ReceivingPeers = false;
                _logger?.Error(e, "Invalid peer(s) - Could not receive peer(s) from the network", null);
            }

            ReceivingPeers = false;
        }

        /// <summary>
        /// Returns the first occurence of the peer. IPeer
        /// implementations may override the equality logic.
        /// </summary>
        /// <param name="peer"></param>
        /// <returns></returns>
        public IPeer GetPeer(IPeer peer)
        {
            return _peers?.FirstOrDefault(p => p.Equals(peer));
        }

        /// <summary>
        /// Adds a peer to the manager and hooks up the callback for
        /// receiving messages from it. It also starts the peers
        /// listening process.
        /// </summary>
        /// <param name="peer">the peer to add</param>
        public IPeer CreatePeerFromConnection(TcpClient client)
        {
            if (client == null)
                return null;
            
            Peer peer = new Peer(_port);
            
            peer.Initialize(client);
            
            // Don't add a peer already in the list
            if (GetPeer(peer) != null)
            {
                _logger.Trace($"[AddPeer] Peer already included - {peer.IpAddress} : {peer.Port}");
                return null;
            }

            /*peer.DistantNodeData.IsBootnode = _bootnodes?.Any(p => p.Equals(peer.DistantNodeData)) ?? false;
            
            if (peer.DistantNodeData.IsBootnode)
                _bootnodePeers.Add(peer);*/
            
            _peers.Add(peer);
            
            peer.PeerAuthentified += PeerOnPeerAuthentified;
            peer.PeerDisconnected += ProcessClientDisconnection;
            
            //PeerAdded?.Invoke(this, new PeerAddedEventArgs { Peer = peer });

            return peer;
        }

        private void PeerOnPeerAuthentified(object sender, EventArgs eventArgs)
        {
            if (sender is Peer peer)
            {
                peer.MessageReceived += ProcessPeerMessage;
                _logger?.Trace("Peer added : " + peer);
                
                PeerAdded?.Invoke(this, new PeerAddedEventArgs { Peer = peer });
            }
        }

        /// <summary>
        /// Creates a Peer.
        /// </summary>
        /// <param name="nodeData"></param>
        /// <returns></returns>
        private async Task<IPeer> CreateAndAddPeer(NodeData nodeData)
        {
            if (nodeData == null)
                return null;
            
            try
            {
                var nodeDialer = new NodeDialer(nodeData.IpAddress.ToString(), nodeData.Port);
                TcpClient peer = await nodeDialer.DialAsync();
                
                // If we successfully connected to the other peer
                // add it to be managed
                if (peer != null)
                {
                    return CreatePeerFromConnection(peer);
                }
            }
            catch (ResponseTimeOutException rex)
            {
                _logger?.Error(rex, rex?.Message + " - "  + nodeData);
            }

            return null;
        }
        
//        private Peer CreatePeerFromConnection(TcpClient client)
//        {
//            Peer netPeer = new Peer();
//            netPeer.Initialize(client);
//                
//            netPeer.MessageReceived += NetPeerOnMessageReceived;
//                
//            _peers.Add(netPeer);
//
//            return netPeer;
//        }
        
//        private void NetPeerOnMessageReceived(object sender, EventArgs eventArgs)
//        {
//            if (eventArgs is PeerMessageReceivedArgs p && p.Peer != null)
//            {
//                //Console.WriteLine("NetManager : Fireing on thread : " + Thread.CurrentThread.ManagedThreadId);
//                MessageReceived?.Invoke(this, new NetMessageReceivedArgs { Message = p.Message});
//            }
//        }
        
        /// <summary>
        /// Removes a peer from the list of peers.
        /// </summary>
        /// <param name="peer">the peer to remove</param>
        public void RemovePeer(IPeer peer)
        {
            if (peer == null)
                return;
            
            _peers.Remove(peer);
            
            _logger?.Trace("Peer removed : " + peer);
            
            PeerRemoved?.Invoke(this, new PeerRemovedEventArgs { Peer = peer });
        }

        public List<IPeer> GetPeers()
        {
            return _peers.Union(_bootnodePeers).ToList();
        }

        /// <summary>
        /// Returns a specified number of random peers from the peer
        /// list.
        /// </summary>
        /// <param name="numPeers">number of peers requested</param>
        public List<NodeData> GetPeers(ushort? numPeers, bool includeBootnodes = true)
        {
            IQueryable<IPeer> peers = _peers.AsQueryable();
            
            if (!includeBootnodes)
                peers = peers.Where(p => !p.IsBootnode);
                
            peers = peers.OrderBy(p => p.Port);

            if (numPeers.HasValue)
                peers = peers.Take(numPeers.Value);

            List<NodeData> peersToReturn = peers
                .Select(peer => new NodeData
                    {
                        IpAddress = peer.IpAddress,
                        Port = peer.Port,
                        IsBootnode = peer.IsBootnode
                    })
                .ToList();

            return peersToReturn;
            
            /*Random rand = new Random();
            List<IPeer> peers = _peers.OrderBy(c => rand.Next()).Select(c => c).ToList();
            List<NodeData> returnPeers = new List<NodeData>();
            
            foreach (var peer in peers)
            {
                NodeData p = new NodeData
                {
                    IpAddress = peer.IpAddress,
                    Port = peer.Port,
                    IsBootnode = peer.IsBootnode
                };
                
                if (!p.IsBootnode)
                    returnPeers.Add(p);

                if (returnPeers.Count == numPeers)
                    break;
            }*/

            //return returnPeers;
        }

        private void WritePeersToDb(List<IPeer> peerList)
        {   
            List<NodeData> peers = new List<NodeData>();

            foreach (var p in peerList)
            {
                NodeData peer = new NodeData
                {
                    IpAddress = p.IpAddress,
                    Port = p.Port,
                    IsBootnode = p.IsBootnode
                };
                peers.Add(peer);
            }
            
            _peerDatabase.WritePeers(peers);
        }
        
        /// <summary>
        /// Callback for when a Peer fires a <see cref="PeerDisconnected"/> event. It unsubscribes
        /// the manager from the events and removes it from the list.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ProcessClientDisconnection(object sender, EventArgs e)
        {
            if (sender != null && e is PeerDisconnectedArgs args && args.Peer != null)
            {
                IPeer peer = args.Peer;
                
                peer.MessageReceived -= ProcessPeerMessage;
                peer.PeerDisconnected -= ProcessClientDisconnection;
                
                _bootnodePeers.Remove(peer);
                
                RemovePeer(args.Peer);
            }
        }

        private void ProcessPeerMessage(object sender, EventArgs e)
        {
            if (sender != null && e is PeerMessageReceivedArgs args && args.Message != null)
            {
                if (args.Message.Type == (int)MessageType.RequestPeers)
                {
                    try
                    {
                        Random rand = new Random();
                        List<IPeer> peers = _peers.OrderBy(c => rand.Next()).Select(c => c).ToList();
                    
                        ReqPeerListData req = ReqPeerListData.Parser.ParseFrom(args.Message.Payload);
                        ushort numPeers = (ushort) req.NumPeers;
                    
                        PeerListData pListData = new PeerListData();

                        foreach (var peer in peers.Where(p => !p.DistantNodeData.Equals(args.Peer.DistantNodeData)))
                        {
                            if (!peer.IsBootnode)
                            {
                                pListData.NodeData.Add(peer.DistantNodeData);
                                if (pListData.NodeData.Count == numPeers)
                                    break;
                            }
                        }

                        var resp = new Message
                        {
                            Type = (int)MessageType.Peers,
                            Length = 1,
                            Payload = pListData.ToByteString().ToByteArray()
                        };
                    }
                    catch (Exception exception)
                    {
                        Console.WriteLine(exception);
                    }

                    // todo
                    //Task.Run(async () => await args.Peer.SendAsync(resp.ToByteArray()));
                }
                else if (args.Message.Type == (int)MessageType.Peers)
                {
                    //Task.Run(() => ReceivePeers(args.Message.Payload));
                }
                else
                {
                    // raise the event so the higher levels can process it.
                    MessageReceived?.Invoke(this, e);
                }
            }
        }

        /// <summary>
        /// This message broadcasts data to all of its peers. This creates and
        /// sends a <see cref="AElfPacketData"/> object with the provided pay-
        /// load and message type.
        /// </summary>
        /// <param name="messageType"></param>
        /// <param name="payload"></param>
        /// <param name="messageId"></param>
        /// <returns></returns>
        public async Task<int> BroadcastMessage(MessageType messageType, byte[] payload, int messageId)
        {
            if (_peers == null || !_peers.Any())
                return 0;

            try
            {
                Message packet = NetRequestFactory.CreateMessage(messageType, payload, messageId);
                return BroadcastMessage(packet);
            }
            catch (Exception e)
            {
                _logger?.Error(e, "Error while sending a message to the peers.");
                return 0;
            }
        }

        public int BroadcastMessage(Message message)
        {
            if (_peers == null || !_peers.Any())
                return 0;

            int count = 0;
            
            try
            {
                //byte[] data = packet.ToByteArray();

                foreach (var peer in _peers)
                {
                    try
                    {
                        peer.EnqueueOutgoing(message); //todo
                        count++;
                    }
                    catch (Exception e) { }
                }
            }
            catch (Exception e)
            {
                _logger?.Error(e, "Error while sending a message to the peers.");
            }

            return count;
        }

        public void Dispose()
        {
            _maintenanceTimer?.Dispose();
        }
    }
}