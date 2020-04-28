using System;
using System.Threading.Tasks;
using AElf.CSharp.Core.Extension;
using AElf.Kernel;
using AElf.OS.Network;
using AElf.OS.Network.Application;
using AElf.OS.Network.Helpers;
using AElf.OS.Network.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;
using Microsoft.Extensions.DependencyInjection;

namespace AElf.OS.Worker
{
    public class PeerReconnectionWorker : AsyncPeriodicBackgroundWorkerBase
    {
        // private readonly IPeerPool _peerPool;
        // private readonly IReconnectionService _reconnectionService;
        // private readonly INetworkService _networkService;

        // private readonly NetworkOptions _networkOptions;

        // public new ILogger<PeerReconnectionWorker> Logger { get; set; }

        public PeerReconnectionWorker(AbpTimer timer, IOptionsSnapshot<NetworkOptions> networkOptions, 
            IServiceScopeFactory serviceScopeFactory)
            : base(timer, serviceScopeFactory)
        {
            timer.Period = networkOptions.Value.PeerReconnectionPeriod;
        }
        
        protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
        {
            var peerPool = workerContext.ServiceProvider.GetService<IPeerPool>();
            var reconnectionService = workerContext.ServiceProvider.GetService<IReconnectionService>();
            var networkService = workerContext.ServiceProvider.GetService<INetworkService>();
            var logger = workerContext.ServiceProvider.GetService<ILogger<PeerReconnectionWorker>>();
            var networkOptions = workerContext.ServiceProvider.GetService<IOptionsSnapshot<NetworkOptions>>().Value;
            await DoReconnectionJobAsync(peerPool, reconnectionService, networkService, logger, networkOptions);
        }

        internal async Task DoReconnectionJobAsync(IPeerPool peerPool, IReconnectionService reconnectionService,
            INetworkService networkService, ILogger<PeerReconnectionWorker> logger,
            NetworkOptions networkOptions)
        {
            CheckNtpClockDrift();
                
            await networkService.CheckPeersHealthAsync();
            
            var peersToConnect = reconnectionService.GetPeersReadyForReconnection(TimestampHelper.GetUtcNow());

            if (peersToConnect.Count <= 0) 
                return;

            foreach (var peerToConnect in peersToConnect)
            {
                string peerEndpoint = peerToConnect.Endpoint;
                if (!AElfPeerEndpointHelper.TryParse(peerEndpoint, out var parsed))
                {
                    if (!reconnectionService.CancelReconnection(peerEndpoint))
                        Logger.LogWarning($"Invalid {peerEndpoint}.");

                    continue;
                }

                // check that we haven't already reconnected to this node
                if (peerPool.FindPeerByEndpoint(parsed) != null)
                {
                    Logger.LogDebug($"Peer {peerEndpoint} already in the pool, no need to reconnect.");

                    if (!reconnectionService.CancelReconnection(peerEndpoint))
                        Logger.LogDebug($"Could not find to {peerEndpoint}.");
                    
                    continue;
                }

                Logger.LogDebug($"Starting reconnection to {peerToConnect.Endpoint}.");

                var connected = false;
                
                try
                {
                    connected = await networkService.AddPeerAsync(peerEndpoint);
                }
                catch (Exception ex)
                {
                    // down the stack the AddPeerAsync rethrows any exception,
                    // in order to continue this job, Exception has to be catched for now.
                    Logger.LogInformation(ex, $"Could not re-connect to {peerEndpoint}.");
                }

                if (connected)
                {
                    Logger.LogDebug($"Reconnection to {peerEndpoint} succeeded.");

                    if (!reconnectionService.CancelReconnection(peerEndpoint))
                        Logger.LogDebug($"Could not find {peerEndpoint}.");
                }
                else
                {
                    var timeExtension = networkOptions.PeerReconnectionPeriod * (int)Math.Pow(2, ++peerToConnect.RetryCount);
                    peerToConnect.NextAttempt = TimestampHelper.GetUtcNow().AddMilliseconds(timeExtension);

                    // if the option is set, verify that the next attempt does not exceed
                    // the maximum reconnection time. 
                    if (networkOptions.MaximumReconnectionTime != 0)
                    {
                        var maxReconnectionDate = peerToConnect.DisconnectionTime +
                                      TimestampHelper.DurationFromMilliseconds(networkOptions.MaximumReconnectionTime);

                        if (peerToConnect.NextAttempt > maxReconnectionDate)
                        {
                            reconnectionService.CancelReconnection(peerEndpoint);
                            Logger.LogDebug($"Maximum reconnection time reached {peerEndpoint}, " +
                                            $"next was {peerToConnect.NextAttempt}.");
                            
                            continue;
                        }
                    }

                    Logger.LogDebug($"Could not connect to {peerEndpoint}, next attempt {peerToConnect.NextAttempt}, " +
                                    $"current retries {peerToConnect.RetryCount}.");
                }
            }

            void CheckNtpClockDrift()
            {
                try
                {
                    networkService.CheckNtpDrift();
                }
                catch (Exception)
                {
                    // swallow any exception, we are not interested in anything else than valid checks. 
                }
            }
        }
    }
}