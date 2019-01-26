using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AElf.Configuration.Config.Chain;
using AElf.Configuration.Config.GRPC;
using AElf.Crosschain.Exceptions;
using AElf.Cryptography.Certificate;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AElf.Crosschain.Server
{
    public class ServerManager
    {
        private Grpc.Core.Server _sideChainServer;
        private Grpc.Core.Server _parentChainServer;
        private CertificateStore _certificateStore;
        private SslServerCredentials _sslServerCredentials;
        private readonly ParentChainBlockInfoRpcServer _parentChainBlockInfoRpcServer;
        private readonly SideChainBlockInfoRpcServer _sideChainBlockInfoRpcServer;
        public ILogger<ServerManager> Logger {get;set;}

        public ServerManager(ParentChainBlockInfoRpcServer parentChainBlockInfoRpcServer, 
            SideChainBlockInfoRpcServer sideChainBlockInfoRpcServer)
        {
            _parentChainBlockInfoRpcServer = parentChainBlockInfoRpcServer;
            _sideChainBlockInfoRpcServer = sideChainBlockInfoRpcServer;
            Logger = NullLogger<ServerManager>.Instance;
            GrpcLocalConfig.ConfigChanged += GrpcLocalConfigOnConfigChanged;
        }

        private void GrpcLocalConfigOnConfigChanged(object sender, EventArgs e)
        {
            Init();
        }
        
        /// <summary>
        /// generate key-certificate pair from pem file 
        /// </summary>
        /// <returns></returns>
        /// <exception cref="CertificateException"></exception>
        /// <exception cref="PrivateKeyException"></exception>
        private KeyCertificatePair GenerateKeyCertificatePair()
        {
            string ch = ChainConfig.Instance.ChainId;
            string certificate = _certificateStore.GetCertificate(ch);
            if(certificate == null)
                throw new CertificateException("Unable to load Certificate.");
            string privateKey = _certificateStore.GetPrivateKey(ch);
            if(privateKey == null)
                throw new PrivateKeyException("Unable to load private key.");
            return new KeyCertificatePair(certificate, privateKey);
        }
        
        /// <summary>
        /// create a new server
        /// </summary>
        /// <returns></returns>
        private Grpc.Core.Server CreateNewSideChainServer()
        {
            var server = new Grpc.Core.Server
            {
                Services = {SideChainBlockInfoRpc.BindService(_sideChainBlockInfoRpcServer)},
                Ports =
                {
                    new ServerPort(GrpcLocalConfig.Instance.LocalServerIP, 
                        GrpcLocalConfig.Instance.LocalSideChainServerPort, _sslServerCredentials)
                }
            };

            return server;
        }

        /// <summary>
        /// create a new server
        /// </summary>
        /// <returns></returns>
        private Grpc.Core.Server CreateNewParentChainServer()
        {
            var server = new Grpc.Core.Server
            {
                Services = {ParentChainBlockInfoRpc.BindService(_parentChainBlockInfoRpcServer)},
                Ports =
                {
                    new ServerPort(GrpcLocalConfig.Instance.LocalServerIP, 
                        GrpcLocalConfig.Instance.LocalParentChainServerPort, _sslServerCredentials)
                }
            };
            return server;
        }
        
        /// <summary>
        /// try to start host service as side chain miner
        /// this server is for indexing service
        /// </summary>
        /// <returns></returns>
        private async Task StartSideChainServer()
        {
            if(!GrpcLocalConfig.Instance.SideChainServer)
                return;

            try
            {
                // for safety, process all request before shutdown 
                await StopSideChainServer();
                _sideChainServer = CreateNewSideChainServer();
                _sideChainServer.Start();
                Logger.LogDebug("Started Side chain server at {0}", GrpcLocalConfig.Instance.LocalSideChainServerPort);
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Exception while start sidechain server.");
                throw;
            }
            
        }

        /// <summary>
        /// stop host service
        /// </summary>
        /// <returns></returns>
        private async Task StopSideChainServer()
        {
            if (_sideChainServer == null)
                return;
            await _sideChainServer.ShutdownAsync();
            _sideChainServer = null;
        }

        /// <summary>
        /// try to start host service as parent chain miner
        /// this server is for recording request from side chain miner
        /// </summary>
        /// <returns></returns>
        private async Task StartParentChainServer()
        {
            if(!GrpcLocalConfig.Instance.ParentChainServer)
                return;
            
            try
            {
                // for safety, process all request before shutdown 
                await StopParentChainServer();
                _parentChainServer = CreateNewParentChainServer();
                _parentChainServer.Start();
                Logger.LogDebug("Started Parent chain server at {0}", GrpcLocalConfig.Instance.LocalParentChainServerPort);
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Exception while start parent chain server.");
                throw;
            }
            
        }

        /// <summary>
        /// stop host service
        /// </summary>
        /// <returns></returns>
        private async Task StopParentChainServer()
        {
            if(_parentChainServer == null)
                return;
            await _parentChainServer.ShutdownAsync();
            _parentChainServer = null;
        }
        
        /// <summary>
        /// init pem storage
        /// and try to start servers if configuration set.
        /// </summary>
        /// <param name="dir"></param>
        public void Init(string dir = "")
        {
            if (!GrpcLocalConfig.Instance.ParentChainServer && !GrpcLocalConfig.Instance.SideChainServer)
                return;
            try
            {
                _certificateStore = dir == "" ? _certificateStore : new CertificateStore(dir);
                var keyCertificatePair = GenerateKeyCertificatePair();
                // create credentials 
                _sslServerCredentials = new SslServerCredentials(new List<KeyCertificatePair> {keyCertificatePair});
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Exception while init ServerManager.");
                throw;
            }
            
            // start servers if possible 
            var startSideChainServerTask = StartSideChainServer();
            var startParentChainServerTask =  StartParentChainServer();
        }

        /// <summary>
        /// stop host services
        /// </summary>
        public void Close()
        {
            // TODO: maybe improvement for NO wait call
            var stopSideChainServerTask = StopSideChainServer();
            var stopParentChainServerTask = StopParentChainServer();
        }
    }
}