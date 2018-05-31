﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AElf.Kernel.Node.Network.Data;
using AElf.Kernel.Node.Network.Peers;
using Google.Protobuf;

namespace AElf.Kernel.Node.Protocol
{
    public class AElfProtocolManager : IProtocolManager
    {
        private IPeerManager _peerManager;
        private List<PendingRequest> _resetEvents = new List<PendingRequest>();
        
        private MainChainNode _node;

        public AElfProtocolManager(IPeerManager peerManager)
        {
            _peerManager = peerManager;
        }
        
        public void Start()
        {
            _peerManager.Start();
            _peerManager.MessageReceived += ProcessPeerMessage;
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
        
        #region Response handling
        
        /// <summary>
        /// Dispatch callback that is executed when a peer receives a message.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void ProcessPeerMessage(object sender, EventArgs e)
        {
            if (sender != null && e is MessageReceivedArgs args && args.Message != null)
            {
                AElfPacketData message = args.Message;
                
                if (message.MsgType == (int)MessageTypes.BroadcastTx)
                {
                    ProcessBroadcastTx(args.Peer, message);
                }

                ClearResetEvent(message.Id);
            }
        }

        private void ClearResetEvent(int eventId)
        {
            var resetEvent = _resetEvents.FirstOrDefault(p => p.Id == eventId);

            if (resetEvent != null)
            {
                resetEvent.ResetEvent.Set();
                _resetEvents.Remove(resetEvent);
            }
        }

        private async void ProcessBroadcastTx(Peer p, AElfPacketData message)
        {
            if (message.MsgType == (int)MessageTypes.BroadcastTx)
            {
                await _node.ReceiveTransaction(message.Payload);
                    
                var resp = new AElfPacketData {
                    Id = message.Id,
                    MsgType = (int)MessageTypes.Ok,
                    Length = 1,
                    Payload = ByteString.CopyFrom(0x01)
                };
                    
                await p.Send(resp.ToByteArray());
            }
        }
        
        #endregion

        #region Requests

        public async Task BroadcastTransaction(byte[] transaction)
        {
            var pendingRequest = BuildRequest();
            
            bool success = await _peerManager.BroadcastMessage(MessageTypes.BroadcastTx, transaction, pendingRequest.Id);
            
            if (success)
                _resetEvents.Add(pendingRequest);

            pendingRequest.ResetEvent.WaitOne();
        }
        
        #endregion

        #region Helpers
        
        private PendingRequest BuildRequest()
        {
            int id = new Random().Next();
            AutoResetEvent resetEvt = new AutoResetEvent(false);

            return new PendingRequest {Id = id, ResetEvent = resetEvt};
        }

        #endregion


        
    }
}