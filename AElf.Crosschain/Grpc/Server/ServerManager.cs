using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AElf.Configuration.Config.GRPC;
using AElf.Crosschain.Exceptions;
using AElf.Cryptography.Certificate;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AElf.Crosschain.Grpc.Server
{
    public class ServerManager
    {
        private global::Grpc.Core.Server _sideChainServer;
        private global::Grpc.Core.Server _parentChainServer;
        private CertificateStore _certificateStore;
        private SslServerCredentials _sslServerCredentials;
        private readonly CrossChainBlockDataRpcServer _crossChainBlockDataRpcServer;
        private readonly SideChainBlockInfoRpcServer _sideChainBlockInfoRpcServer;
        public ILogger<ServerManager> Logger {get;set;}

        public ServerManager(CrossChainBlockDataRpcServer crossChainBlockDataRpcServer, 
            SideChainBlockInfoRpcServer sideChainBlockInfoRpcServer)
        {
            _crossChainBlockDataRpcServer = crossChainBlockDataRpcServer;
            _sideChainBlockInfoRpcServer = sideChainBlockInfoRpcServer;
            Logger = NullLogger<ServerManager>.Instance;
        }

         
        /// <summary>
        /// generate key-certificate pair from pem file 
        /// </summary>
        /// <returns></returns>
        /// <exception cref="CertificateException"></exception>
        /// <exception cref="PrivateKeyException"></exception>
        private KeyCertificatePair GenerateKeyCertificatePair(int chainId)
        {
            string certificate = _certificateStore.GetCertificate(chainId.ToString());
            if(certificate == null)
                throw new CertificateException("Unable to load Certificate.");
            string privateKey = _certificateStore.GetPrivateKey(chainId.ToString());
            if(privateKey == null)
                throw new PrivateKeyException("Unable to load private key.");
            return new KeyCertificatePair(certificate, privateKey);
        }
        
        /// <summary>
        /// create a new server
        /// </summary>
        /// <returns></returns>
        private global::Grpc.Core.Server CreateNewSideChainServer()
        {
            var server = new global::Grpc.Core.Server
            {
                //Services = {SideChainRpc.BindService(_sideChainBlockInfoRpcServer)},
//                Ports =
//                {
//                    new ServerPort(GrpcLocalConfig.Instance.LocalServerIP, 
//                        GrpcLocalConfig.Instance.LocalSideChainServerPort, _sslServerCredentials)
//                }
            };

            return server;
        }

        /// <summary>
        /// create a new server
        /// </summary>
        /// <returns></returns>
        private global::Grpc.Core.Server CreateNewParentChainServer()
        {
            var server = new global::Grpc.Core.Server
            {
                //Services = {ParentChainRpc.BindService(_crossChainBlockDataRpcServer)},
//                Ports =
//                {
//                    new ServerPort(GrpcLocalConfig.Instance.LocalServerIP, 
//                        GrpcLocalConfig.Instance.LocalParentChainServerPort, _sslServerCredentials)
//                }
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
//            if(!GrpcLocalConfig.Instance.SideChainServer)
//                return;

            try
            {
                // for safety, process all request before shutdown 
                await StopSideChainServer();
                _sideChainServer = CreateNewSideChainServer();
                _sideChainServer.Start();
                //Logger.LogDebug("Started Side chain server at {0}", GrpcLocalConfig.Instance.LocalSideChainServerPort);
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
//            if(!GrpcLocalConfig.Instance.ParentChainServer)
//                return;
            
            try
            {
                // for safety, process all request before shutdown 
                await StopParentChainServer();
                _parentChainServer = CreateNewParentChainServer();
                _parentChainServer.Start();
                //Logger.LogDebug("Started Parent chain server at {0}", GrpcLocalConfig.Instance.LocalParentChainServerPort);
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
        /// <param name="chainId"></param>
        /// <param name="dir"></param>
        public void Init(int chainId, string dir = "")
        {
//            if (!GrpcLocalConfig.Instance.ParentChainServer && !GrpcLocalConfig.Instance.SideChainServer)
//                return;
            try
            {
                _certificateStore = dir == "" ? _certificateStore : new CertificateStore(dir);
                var keyCertificatePair = GenerateKeyCertificatePair(chainId);
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