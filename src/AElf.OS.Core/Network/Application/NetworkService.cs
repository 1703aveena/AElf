using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf.Kernel;
using AElf.OS.Network.Infrastructure;
using AElf.Types;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Volo.Abp.DependencyInjection;

namespace AElf.OS.Network.Application
{
    public class NetworkService : INetworkService, ISingletonDependency
    {
        private readonly IPeerPool _peerPool;
        private readonly ITaskQueueManager _taskQueueManager;

        public ILogger<NetworkService> Logger { get; set; }

        public NetworkService(IPeerPool peerPool, ITaskQueueManager taskQueueManager)
        {
            _peerPool = peerPool;
            _taskQueueManager = taskQueueManager;

            Logger = NullLogger<NetworkService>.Instance;
        }

        public async Task<bool> AddPeerAsync(string address)
        {
            return await _peerPool.AddPeerAsync(address);
        }

        public async Task<bool> RemovePeerAsync(string address)
        {
            return await _peerPool.RemovePeerByAddressAsync(address);
        }

        public List<string> GetPeerIpList()
        {
            return _peerPool.GetPeers(true).Select(p => p.PeerIpAddress).ToList();
        }

        public List<IPeer> GetPeers()
        {
            return _peerPool.GetPeers(true).ToList(); 
        }

        public void BroadcastAnnounce(BlockHeader blockHeader, bool hasFork)
        {
            var blockHash = blockHeader.GetHash();
            if (_peerPool.RecentBlockHeightAndHashMappings.TryGetValue(blockHeader.Height, out var recentBlock) &&
                recentBlock.BlockHash == blockHash)
            {
                Logger.LogDebug($"BlockHeight: {blockHeader.Height}, BlockHash: {blockHash} has been broadcast.");
                return;
            }
            
            _peerPool.AddRecentBlockHeightAndHash(blockHeader.Height, blockHash, hasFork);
            
            var announce = new PeerNewBlockAnnouncement
            {
                BlockHash = blockHash,
                BlockHeight = blockHeader.Height,
                HasFork = hasFork
            };
            
            _taskQueueManager.Enqueue(async () =>
            {
                foreach (var peer in _peerPool.GetPeers().Where(p => p.CanStreamAnnounces))
                {
                    try
                    {
                        await peer.AnnounceAsync(announce);
                    }
                    catch (NetworkException ex)
                    {
                        Logger.LogError(ex, $"Error while announcing to {peer}.");
                        await HandleNetworkException(peer, ex);
                    }
                }
                
            }, NetworkConstants.AnnouncementBroadcastQueueName);
        }

        public Task BroadcastPreLibAnnounceAsync(long blockHeight, Hash blockHash,int preLibCount)
        {
            if (!_peerPool.HasBlock(blockHeight, blockHash)) return Task.CompletedTask;

            _peerPool.AddPreLibBlockHeightAndHash(blockHeight, blockHash, preLibCount);
            
            var announce = new PeerPreLibAnnouncement
            {
                BlockHash = blockHash,
                BlockHeight = blockHeight,
                PreLibCount = preLibCount
            };

            _taskQueueManager.Enqueue(async () =>
            {
                foreach (var peer in _peerPool.GetPeers().Where(p => p.CanStreamPreLibAnnounces))
                {
                    try
                    {
                        await peer.PreLibAnnounceAsync(announce);
                    }
                    catch (NetworkException ex)
                    {
                        Logger.LogError(ex, $"Error while announcing pre lib to {peer}.");
                        await HandleNetworkException(peer, ex);
                    }
                }
                
            }, NetworkConstants.PreLibAnnouncementBroadcastQueueName);
            return Task.CompletedTask;
        }

        public Task BroadcastPreLibConfirmAnnounceAsync(long blockHeight, Hash blockHash, int preLibCount)
        {
            if (!_peerPool.HasBlock(blockHeight,blockHash)) return Task.CompletedTask;
            
            _peerPool.AddPreLibBlockHeightAndHash(blockHeight, blockHash, preLibCount);

            var preLibConfirm = new PeerPreLibConfirmAnnouncement
            {
                BlockHash = blockHash,
                BlockHeight = blockHeight,
                PreLibCount = preLibCount
            };
            
           _taskQueueManager.Enqueue(async () =>
           {
               foreach (var peer in _peerPool.GetPeers().Where(p => p.CanStreamPreLibConfirmAnnounces))
               {
                   try
                   {
                       await peer.PreLibConfirmAnnounceAsync(preLibConfirm);
                   }
                   catch (NetworkException ex)
                   {
                       Logger.LogError(ex, $"Error while announcing pre lib confirm to {peer}.");
                       await HandleNetworkException(peer, ex);
                   }
               }
                    
           }, NetworkConstants.PreLibConfirmAnnouncementBroadcastQueueName);
           return Task.CompletedTask;
        }
        
        public void BroadcastTransaction(Transaction tx)
        {
            _taskQueueManager.Enqueue(async () =>
            {
                foreach (var peer in _peerPool.GetPeers().Where(p => p.CanStreamTransactions))
                {
                    try
                    {
                        await peer.SendTransactionAsync(tx);
                    }
                    catch (NetworkException ex)
                    {
                        Logger.LogError(ex, "Error while sending transaction.");
                        await HandleNetworkException(peer, ex);
                    }
                }
                
            }, NetworkConstants.TransactionBroadcastQueueName);
        }

        public async Task<List<BlockWithTransactions>> GetBlocksAsync(Hash previousBlock, int count, 
            string peerPubKey = null)
        {
            var peers = SelectPeers(peerPubKey);

            var blocks = await RequestAsync(peers, p => p.GetBlocksAsync(previousBlock, count), 
                blockList => blockList != null && blockList.Count > 0, 
                peerPubKey);

            if (blocks != null && (blocks.Count == 0 || blocks.Count != count))
                Logger.LogWarning($"Block count miss match, asked for {count} but got {blocks.Count}");

            return blocks;
        }
        
        private List<IPeer> SelectPeers(string peerPubKey)
        {
            List<IPeer> peers = new List<IPeer>();
            
            // Get the suggested peer 
            IPeer suggestedPeer = _peerPool.FindPeerByPublicKey(peerPubKey);

            if (suggestedPeer == null)
                Logger.LogWarning("Could not find suggested peer");
            else
                peers.Add(suggestedPeer);
            
            // Get our best peer
            IPeer bestPeer = _peerPool.GetBestPeer();
            
            if (bestPeer == null)
                Logger.LogWarning("No best peer.");
            else if (bestPeer.PubKey != peerPubKey)
                peers.Add(bestPeer);
            
            Random rnd = new Random();
            
            // Fill with random peers.
            List<IPeer> randomPeers = _peerPool.GetPeers()
                .Where(p => p.PubKey != peerPubKey && (bestPeer == null || p.PubKey != bestPeer.PubKey))
                .OrderBy(x => rnd.Next())
                .Take(NetworkConstants.DefaultMaxRandomPeersPerRequest)
                .ToList();
            
            peers.AddRange(randomPeers);
            
            Logger.LogDebug($"Selected {peers.Count} for the request.");

            return peers;
        }
        
        public async Task<BlockWithTransactions> GetBlockByHashAsync(Hash hash, string peer = null)
        {
            Logger.LogDebug($"Getting block by hash, hash: {hash} from {peer}.");
            
            var peers = SelectPeers(peer);
            return await RequestAsync(peers, p => p.RequestBlockAsync(hash), blockWithTransactions => blockWithTransactions != null, peer);
        }

        private async Task<(IPeer, T)> DoRequest<T>(IPeer peer, Func<IPeer, Task<T>> func) where T : class
        {
            try
            {
                Logger.LogDebug($"before request send to {peer.PeerIpAddress}.");
                var res = await func(peer);
                Logger.LogDebug($"request send to {peer.PeerIpAddress}.");
                
                return (peer, res);
            }
            catch (NetworkException ex)
            {
                Logger.LogError(ex, $"Error while requesting block from {peer.PeerIpAddress}.");
                await HandleNetworkException(peer, ex);
            }
            
            return (peer, null);
        }

        private async Task HandleNetworkException(IPeer peer, NetworkException exception)
        {
            if (exception.ExceptionType == NetworkExceptionType.Unrecoverable)
            {
                await _peerPool.RemovePeerAsync(peer.PubKey, false);
            }
            else if (exception.ExceptionType == NetworkExceptionType.PeerUnstable)
            {
                Logger.LogError($"Queuing peer for reconnection {peer.PeerIpAddress}.");
                
                QueueNetworkTask(async () => {
                    if (peer.IsReady) // peer recovered already
                        return;
                
                    var success = await peer.TryWaitForStateChangedAsync();

                    if (!success)
                        await _peerPool.RemovePeerAsync(peer.PubKey, false);
                });
            }
            else if (exception.ExceptionType == NetworkExceptionType.AnnounceStream)
            {
                Logger.LogDebug($"Queuing peer for announcement stream recreation {peer.PeerIpAddress}.");
                
                QueueNetworkTask(() =>
                {
                    if (!peer.IsReady)
                    {
                        Logger.LogDebug($"Peer {peer} is unstable, will not start streaming.");
                        return Task.CompletedTask;
                    }
                        
                    if (!peer.CanStreamAnnounces)
                    {
                        peer.StartAnnouncementStreaming();
                        Logger.LogDebug($"Started announcement stream {peer.PeerIpAddress}.");
                    }
                    else
                    {
                        Logger.LogDebug($"Already started announcement stream {peer.PeerIpAddress}.");
                    }
                    
                    return Task.CompletedTask;
                });
            }
            else if (exception.ExceptionType == NetworkExceptionType.PreLibAnnounceStream)
            {
                Logger.LogDebug($"Queuing peer for pre lib announcement stream recreation {peer.PeerIpAddress}.");
                
                QueueNetworkTask(() =>
                {
                    if (!peer.CanStreamPreLibAnnounces)
                    {
                        peer.StartPreLibAnnouncementStreaming();
                        Logger.LogDebug($"Started pre lib announcement stream {peer.PeerIpAddress}.");
                    }
                    else
                    {
                        Logger.LogDebug($"Already started pre lib announcement stream {peer.PeerIpAddress}.");
                    }
                    
                    return Task.CompletedTask;
                });
            }
            else if (exception.ExceptionType == NetworkExceptionType.PreLibConfirmAnnounceStream)
            {
                Logger.LogDebug($"Queuing peer for pre lib confirm announcement stream recreation {peer.PeerIpAddress}.");
                
                QueueNetworkTask(() =>
                {
                    if (!peer.CanStreamPreLibConfirmAnnounces)
                    {
                        peer.StartPreLibConfirmAnnouncementStreaming();
                        Logger.LogDebug($"Started pre lib confirm announcement stream {peer.PeerIpAddress}.");
                    }
                    else
                    {
                        Logger.LogDebug($"Already started pre lib confirm announcement stream {peer.PeerIpAddress}.");
                    }
                    
                    return Task.CompletedTask;
                });
            }
            else if (exception.ExceptionType == NetworkExceptionType.TransactionStream)
            {
                Logger.LogDebug($"Queuing peer for transaction stream recreation {peer.PeerIpAddress}.");

                QueueNetworkTask(() =>
                {
                    if (!peer.IsReady)
                    {
                        Logger.LogDebug($"Peer {peer} is unstable, will not start streaming.");
                        return Task.CompletedTask;
                    }
                    
                    if (!peer.CanStreamTransactions)
                    {
                        peer.StartTransactionStreaming();
                        Logger.LogDebug($"Started transaction stream {peer.PeerIpAddress}.");
                    }
                    else
                    {
                        Logger.LogDebug($"Already started transaction stream {peer.PeerIpAddress}.");
                    }
                    
                    return Task.CompletedTask;
                });
            }
        }

        private void QueueNetworkTask(Func<Task> task)
        {
            _taskQueueManager.Enqueue(task, NetworkConstants.PeerReconnectionQueueName);
        }

        private async Task<T> RequestAsync<T>(List<IPeer> peers, Func<IPeer, Task<T>> func,
            Predicate<T> validationFunc, string suggested) where T : class
        {
            if (peers.Count <= 0)
            {
                Logger.LogWarning("Peer list is empty.");
                return null;
            }
            
            var taskList = peers.Select(peer => DoRequest(peer, func)).ToList();
            
            Task<(IPeer, T)> finished = null;
            
            while (taskList.Count > 0)
            {
                var next = await Task.WhenAny(taskList);

                if (validationFunc(next.Result.Item2))
                {
                    finished = next;
                    break;
                }

                taskList.Remove(next);
            }

            if (finished == null)
            {
                Logger.LogDebug($"No peer succeeded.");
                return null;
            }

            IPeer taskPeer = finished.Result.Item1;
            T taskRes = finished.Result.Item2;
            
            UpdateBestPeer(taskPeer);
            
            if (suggested != taskPeer.PubKey)
                Logger.LogWarning($"Suggested {suggested}, used {taskPeer.PubKey}");
            
            Logger.LogDebug($"First replied {taskRes} : {taskPeer}.");

            return taskRes;
        }

        private void UpdateBestPeer(IPeer taskPeer)
        {
            if (taskPeer.IsBest) 
                return;
            
            Logger.LogDebug($"New best peer found: {taskPeer}.");

            foreach (var peerToReset in _peerPool.GetPeers(true))
            {
                peerToReset.IsBest = false;
            }
                
            taskPeer.IsBest = true;
        }

        public Task<long> GetBestChainHeightAsync(string peerPubKey = null)
        {
            var peer = !peerPubKey.IsNullOrEmpty()
                ? _peerPool.FindPeerByPublicKey(peerPubKey)
                : _peerPool.GetPeers().OrderByDescending(p => p.CurrentBlockHeight).FirstOrDefault();
            return Task.FromResult(peer?.CurrentBlockHeight ?? 0);
        }
    }
}