﻿using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using AElf.Network;
using AElf.Network.Peers;
using AElf.Network.Data;
using AElf.Network.Connection;
using Google.Protobuf;
using NLog;

namespace AElf.Kernel.Node
{
    public class P2P:IP2P
    {
        private readonly ILogger _logger;
        private readonly INetworkManager _netManager;

        private BlockingCollection<NetMessageReceivedArgs> _messageQueue =
            new BlockingCollection<NetMessageReceivedArgs>();

        private P2PHandler _handler;

        public P2P(P2PHandler handler, ILogger logger, INetworkManager netManager)
        {
            _handler = handler;
            _logger = logger;
            _netManager = netManager;
            _netManager.MessageReceived += ProcessPeerMessage;
        }

        public async Task ProcessLoop()
        {
            try
            {
                while (true)
                {
                    var args = _messageQueue.Take();

                    var message = args.Message;
                    var msgType = (MessageType) message.Type;

                    _logger?.Trace($"Handling message {message}");

                    if (msgType == MessageType.RequestBlock)
                    {
                        await HandleBlockRequest(message, args.PeerMessage);
                    }
                    else if (msgType == MessageType.TxRequest)
                    {
                        await HandleTxRequest(message, args.PeerMessage);
                    }
                }
            }
            catch (Exception e)
            {
                _logger?.Trace(e, "Error while dequeuing.");
            }
        }

        internal async Task HandleBlockRequest(Message message, PeerMessageReceivedArgs args)
        {
            try
            {
                var breq = BlockRequest.Parser.ParseFrom(message.Payload);
                var block = await _handler.GetBlockAtHeight(breq.Height);
                var req = NetRequestFactory.CreateMessage(MessageType.Block, block.ToByteArray());

                args.Peer.EnqueueOutgoing(req);

                _logger?.Trace("Send block " + block.GetHash().ToHex() + " to " + args.Peer);
            }
            catch (Exception e)
            {
                _logger?.Trace(e, "Error while during HandleBlockRequest.");
            }
        }

        private async Task HandleTxRequest(Message message, PeerMessageReceivedArgs args)
        {
            string hash = null;

            try
            {
                var breq = TxRequest.Parser.ParseFrom(message.Payload);
                hash = breq.TxHash.ToByteArray().ToHex();
                var tx = await _handler.GetTransaction(breq.TxHash);
                if (!(tx is Transaction t))
                {
                    _logger?.Trace("Could not find transaction: ", hash);
                    return;
                }

                var req = NetRequestFactory.CreateMessage(MessageType.Tx, t.ToByteArray());
                args.Peer.EnqueueOutgoing(req);

                _logger?.Trace("Send tx " + t.GetHash().ToHex() + " to " + args.Peer + "(" + t.ToByteArray().Length +
                               " bytes)");
            }
            catch (Exception e)
            {
                _logger?.Trace(e, $"Transaction request failed. Hash : {hash}");
            }
        }

        private void ProcessPeerMessage(object sender, EventArgs e)
        {
            if (sender != null && e is NetMessageReceivedArgs args && args.Message != null)
            {
                _messageQueue.Add(args);
            }
        }
    }
}