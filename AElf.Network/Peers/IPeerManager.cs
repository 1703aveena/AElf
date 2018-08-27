﻿using System;
using System.Threading.Tasks;
using Community.AspNetCore.JsonRpc;
using Newtonsoft.Json.Linq;

namespace AElf.Network.Peers
{
    public interface IPeerManager : IDisposable
    {
        event EventHandler PeerEvent;
        
        void Start();
        
        Task<JObject> GetPeers();
    }
}