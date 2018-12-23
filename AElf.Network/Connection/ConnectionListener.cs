﻿using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AElf.Network.Connection
{
    public class IncomingConnectionArgs : EventArgs
    {
        public TcpClient Client { get; set; }
    }
    
    public class ConnectionListener : IConnectionListener
    {
        public ILogger<ConnectionListener> Logger {get;set;}
        public event EventHandler IncomingConnection;
        public event EventHandler ListeningStopped;

        private TcpListener _tcpListener;

        public ConnectionListener()
        {
            Logger = NullLogger<ConnectionListener>.Instance;
        }

        public async Task StartListening(int port)
        {
            try
            {
                _tcpListener = new TcpListener(IPAddress.Any, port);
                _tcpListener.Start();
            
                while (true)
                {
                    await AwaitConnection(_tcpListener);
                }
            }
            catch (Exception ex)
            {
                Logger.LogTrace(ex, "Connection listening stopped, no new connections can be made.");
                ListeningStopped?.Invoke(this, EventArgs.Empty);
            }
        }
        
        private async Task AwaitConnection(TcpListener tcpListener)
        {
            TcpClient client = await tcpListener.AcceptTcpClientAsync();
            LogConnection(client);
            IncomingConnection?.Invoke(this, new IncomingConnectionArgs { Client = client});
        }
        
        private void LogConnection(TcpClient client)
        {
            IPEndPoint remoteIpEndPoint = client?.Client?.RemoteEndPoint as IPEndPoint;
            IPEndPoint localIpEndPoint = client?.Client?.LocalEndPoint as IPEndPoint;
            
            Logger.LogTrace($"[{localIpEndPoint?.Address}:{localIpEndPoint?.Port}] Accepted a connection from {remoteIpEndPoint?.Address}:{remoteIpEndPoint?.Port}.");
        }

        #region Closing and disposing

        public void Close()
        {
            Dispose();
        }
        
        public void Dispose()
        {
            // This will cause an IOException in the read loop
            // but since IsConnected is switched to false, it 
            // will not fire the disconnection exception.
            _tcpListener.Stop();
        }

        #endregion
    }
}