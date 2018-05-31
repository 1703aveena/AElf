﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf.Kernel.Node.Network.Config;
using AElf.Kernel.Node.Network.Data;
using AElf.Kernel.Node.Network.Peers.Exceptions;
using Google.Protobuf;
using NLog;

namespace AElf.Kernel.Node.Network.Peers
{
    public class PeerManager : IPeerManager
    {
        private readonly IAElfNetworkConfig _networkConfig;
        private readonly IAElfServer _server;
        private readonly IPeerDatabase _peerDatabase;
        private readonly ILogger _logger;
        private readonly List<IPeer> _peers = new List<IPeer>();

        private readonly NodeData _nodeData;
        
        private MainChainNode _node;

        public PeerManager(IAElfServer server, IPeerDatabase peerDatabase, IAElfNetworkConfig config, ILogger logger)
        {
            _networkConfig = config;
            _logger = logger;
            _server = server;
            _peerDatabase = peerDatabase;

            _nodeData = new NodeData()
            {
                IpAddress = config.Host,
                Port = config.Port
            };
        }
        
        /// <summary>
        /// Temporary solution, this is used for injecting a
        /// reference to the node.
        /// todo : remove dependency on the node
        /// </summary>
        /// <param name="node"></param>
        public void SetCommandContext(MainChainNode node)
        {
            _node = node;
        }

        private void HandleConnection(object sender, EventArgs e)
        {
            if (sender != null && e is ClientConnectedArgs args)
            {
                AddPeer(args.NewPeer);
            }
        }

        /// <summary>
        /// This method start the server that listens for incoming
        /// connections and sets up the manager.
        /// </summary>
        public void Start()
        {
            Task.Run(() => _server.StartAsync());
            Task.Run(Setup);
            
            _server.ClientConnected += HandleConnection;
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
                foreach (var peerString in _networkConfig.Peers)
                {
                    // Parse the IP and port
                    string[] split = peerString.Split(':');

                    if (split.Length != 2)
                        continue;
                    
                    ushort port = ushort.Parse(split[1]);
                    
                    NodeData peer = new NodeData();
                    peer.IpAddress = split[0];
                    peer.Port = port;
                    
                    IPeer p = new Peer(_nodeData, peer);
                    
                    bool success = await p.DoConnectAsync();

                    // If we succesfully connected to the other peer 
                    // add it to be managed.
                    if (success)
                    {
                        AddPeer(p);
                    }
                }
            }

            var dbNodeData = _peerDatabase.ReadPeers();

            // Parse List<NodeData> to List<IPeer>
            var dbPeers = new List<IPeer>();
            foreach (var p in dbNodeData)
            {
                dbPeers.Add(new Peer(_nodeData, p));
            }
            
            if (dbPeers.Count > 0)
            {
                foreach (var peer in dbPeers)
                {
                    if (_peers.Contains(peer))
                        continue;
                    
                    peer.SetNodeData(_nodeData); // todo temp

                    try
                    {
                        bool success = await peer.DoConnectAsync();
                    
                        // If we successfully connected to the other peer
                        // add it to be managed
                        if (success)
                        {
                            AddPeer(peer);
                        }
                    }
                    catch (ResponseTimeOutException rex)
                    {
                        _logger.Error(rex, rex?.Message + " - "  + peer);
                    }
                }
            }
        }

        /// <summary>
        /// Adds a peer to the manager and hooks up the callback for
        /// receiving messages from it. It also starts the peers
        /// listening process.
        /// </summary>
        /// <param name="peer">the peer to add</param>
        public void AddPeer(IPeer peer)
        {
            // Temporary solution to checking if peer is already in _peers
            bool exists = false;
            foreach (var p in _peers)
            {
                exists = p.IpAddress == peer.IpAddress && p.Port == peer.Port;
            }
            if (exists) return;
            
            _peers.Add(peer);
            peer.MessageReceived += ProcessPeerMessage;
            peer.PeerDisconnected += ProcessClientDisconnection;

            _logger.Trace("Peer added : " + peer);

            Task.Run(peer.StartListeningAsync);
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
                args.Peer.MessageReceived -= ProcessPeerMessage;
                args.Peer.PeerDisconnected -= ProcessClientDisconnection;
                RemovePeer(args.Peer);

                ushort missingPeers = (ushort) (8 - _peers.Count);
                
// TRIGGER PEER MAINTENANCE
            }
        }

        /// <summary>
        /// Removes a peer from the list of peers.
        /// </summary>
        /// <param name="peer">the peer to remove</param>
        public void RemovePeer(IPeer peer)
        {
            _peers.Remove(peer);
            _logger.Trace("Peer removed : " + peer);
        }

        /// <summary>
        /// Returns a specified number of random peers from the peer
        /// list.
        /// </summary>
        /// <param name="numPeers">number of peers requested</param>
        public List<NodeData> GetPeers(ushort numPeers)
        {
            List<NodeData> peers = new List<NodeData>();
            
            for (ushort i = 0; i < numPeers - 1; i++)
            {
                try
                {
                    NodeData p = new NodeData();
                    p.IpAddress = _peers[i].IpAddress;
                    p.Port = _peers[i].Port;
                    peers.Add(p);
                }
                catch (Exception e)
                {
                    _logger.Error(e, e?.Message);
                    break;
                }
            }

            return peers;
        }

        /// <summary>
        /// Callback that is executed when a peer receives a message.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void ProcessPeerMessage(object sender, EventArgs e)
        {
            if (sender != null && e is MessageReceivedArgs args && args.Message != null)
            {
                if (args.Message.MsgType == (int)MessageTypes.BroadcastTx)
                {
                    await _node.ReceiveTransaction(args.Message.Payload);
                    
                    var resp = new AElfPacketData
                    {
                        MsgType = (int)MessageTypes.Ok,
                        Length = 1,
                        Payload = ByteString.CopyFrom(0x01)
                    };
                    
                    await args.Peer.SendDataAsync(resp.ToByteArray());
                }
                else if (args.Message.MsgType == (int) MessageTypes.Ok)
                {
                    ;
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
        /// <returns></returns>
        public async Task<bool> BroadcastMessage(MessageTypes messageType, byte[] payload)
        {
            if (_peers == null || !_peers.Any())
                return false;

            try
            {
                AElfPacketData packetData = new AElfPacketData
                {
                    MsgType = (int)messageType,
                    Length = payload.Length,
                    Payload = ByteString.CopyFrom(payload)
                };

                byte[] data = packetData.ToByteArray();

                bool allGood = true;
                foreach (var peer in _peers)
                {
                    AElfPacketData d = await peer.SendRequestAsync(data);
                    bool bb = Convert.ToBoolean(d.Payload[0]);
                    allGood = allGood && bb;
                }

                return allGood;
            }
            catch (Exception e)
            {
                _logger.Error(e, "Error while sending a message to the peers");
                return false;
            }
        }
    }
}