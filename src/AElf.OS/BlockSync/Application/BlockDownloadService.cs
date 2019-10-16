using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf.Kernel;
using AElf.OS.BlockSync.Dto;
using AElf.OS.BlockSync.Events;
using AElf.OS.BlockSync.Exceptions;
using AElf.OS.BlockSync.Infrastructure;
using AElf.OS.BlockSync.Types;
using AElf.OS.Network;
using AElf.OS.Network.Application;
using AElf.Types;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Volo.Abp.EventBus.Local;

namespace AElf.OS.BlockSync.Application
{
    public class BlockDownloadService : IBlockDownloadService
    {
        private readonly INetworkService _networkService;
        private readonly IBlockSyncAttachService _blockSyncAttachService;
        private readonly IBlockSyncQueueService _blockSyncQueueService;
        private readonly IBlockSyncStateProvider _blockSyncStateProvider;

        public ILocalEventBus LocalEventBus { get; set; }

        public ILogger<BlockDownloadService> Logger { get; set; }

        private const int PeerCheckMinimumCount = 15;

        public BlockDownloadService(INetworkService networkService,
            IBlockSyncAttachService blockSyncAttachService,
            IBlockSyncQueueService blockSyncQueueService,
            IBlockSyncStateProvider blockSyncStateProvider)
        {
            Logger = NullLogger<BlockDownloadService>.Instance;
            LocalEventBus = NullLocalEventBus.Instance;

            _networkService = networkService;
            _blockSyncAttachService = blockSyncAttachService;
            _blockSyncQueueService = blockSyncQueueService;
            _blockSyncStateProvider = blockSyncStateProvider;
        }

        /// <summary>
        /// Download and attach blocks
        /// UseSuggestedPeer == true: Download blocks from suggested peer directly;
        /// Target download height > peer lib height, download blocks from suggested peer;
        /// Target download height <= peer lib height, select a random peer to download.
        /// </summary>
        /// <param name="downloadBlockDto"></param>
        /// <returns></returns>
        public async Task<DownloadBlocksResult> DownloadBlocksAsync(DownloadBlockDto downloadBlockDto)
        {
            var downloadResult = new DownloadBlocksResult();
            var peerPubkey = downloadBlockDto.SuggestedPeerPubkey;

            try
            {
                if (UseSuggestedPeer(downloadBlockDto))
                {
                    downloadResult = await DownloadBlocksAsync(peerPubkey, downloadBlockDto);
                }
                else
                {
                    // If cannot get the blocks, there should be network problems or bad peer,
                    // because we have selected peer with lib height greater than or equal to the target height.
                    // 1. network problems, need to retry from other peer.
                    // 2. not network problems, this peer or the last peer is bad peer, we need to remove it.
                    var downloadTargetHeight =
                        downloadBlockDto.PreviousBlockHeight + downloadBlockDto.MaxBlockDownloadCount;
                    var exceptedPeers = new List<string> {_blockSyncStateProvider.LastRequestPeerPubkey};
                    var retryTimes = 2;

                    while (true)
                    {
                        peerPubkey = GetRandomPeerPubkey(downloadBlockDto.SuggestedPeerPubkey, downloadTargetHeight,
                            exceptedPeers);

                        downloadResult = await DownloadBlocksAsync(peerPubkey, downloadBlockDto);;

                        if (downloadResult.Success || retryTimes <= 0)
                            break;

                        exceptedPeers.Add(peerPubkey);
                        retryTimes--;
                    }

                    if (downloadResult.Success && downloadResult.DownloadBlockCount == 0)
                    {
                        await CheckBadPeerAsync(peerPubkey, downloadBlockDto.PreviousBlockHash,
                            downloadBlockDto.PreviousBlockHeight);
                    }
                }
            }
            catch (BlockDownloadException e)
            {
                await LocalEventBus.PublishAsync(new BadPeerFoundEventData
                {
                    BlockHash = e.BlockHash,
                    BlockHeight = e.BlockHeight,
                    PeerPubkey = e.PeerPubkey
                });
            }
            
            return downloadResult;
        }

        private async Task<bool?> CheckIrreversibleBlockHashAsync(Hash blockHash, long blockHeight)
        {
            var peers = _networkService.GetPeers(false)
                .Where(p => p.SyncState == SyncState.Finished && 
                            p.LastKnownLibHeight >= blockHeight)
                .ToList();
            bool? checkResult = null;

            // Make sure we have enough peer to check the block
            if (peers.Count() > PeerCheckMinimumCount)
            {
                var getBlockSuccessCount = 0;
                var getBlockFailedCount = 0;

                foreach (var peer in peers)
                {
                    var result = await _networkService.GetBlocksAsync(blockHash, 1, peer.Pubkey);
                    if (result.Success)
                    {
                        if (result.Payload != null && result.Payload.Count != 0)
                        {
                            getBlockSuccessCount++;
                        }
                        else
                        {
                            getBlockFailedCount++;
                        }
                    }
                }

                var confirmCount = 2 * peers.Count() / 3 + 1;
                if (getBlockSuccessCount >= confirmCount)
                {
                    checkResult = true;
                }
                else if (getBlockFailedCount >= confirmCount)
                {
                    checkResult = false;
                }
            }

            return checkResult;
        }


        private async Task CheckBadPeerAsync(string peerPubkey, Hash downloadPreviousBlockHash,
            long downloadPreviousBlockHeight)
        {
            var checkResult =
                await CheckIrreversibleBlockHashAsync(downloadPreviousBlockHash, downloadPreviousBlockHeight);

            if (checkResult.HasValue)
            {
                var wrongPeerPubkey = checkResult.Value ? peerPubkey : _blockSyncStateProvider.LastRequestPeerPubkey;
                Logger.LogWarning(
                    $"Wrong irreversible block: {wrongPeerPubkey}, block hash: {downloadPreviousBlockHash}, block height: {downloadPreviousBlockHeight}");
                throw new BlockDownloadException(downloadPreviousBlockHash, downloadPreviousBlockHeight,
                    wrongPeerPubkey);
            }
        }

        private bool UseSuggestedPeer(DownloadBlockDto downloadBlockDto)
        {
            if (downloadBlockDto.UseSuggestedPeer)
                return true;
            
            var suggestedPeer = _networkService.GetPeerByPubkey(downloadBlockDto.SuggestedPeerPubkey);
            var downloadTargetHeight = downloadBlockDto.PreviousBlockHeight + downloadBlockDto.MaxBlockDownloadCount;
            if (downloadTargetHeight > suggestedPeer.LastKnownLibHeight)
                return true;

            return false;
        }

        private string GetRandomPeerPubkey(string defaultPeerPubkey, long peerLibHeight, List<string> exceptedPeers)
        {
            var random = new Random();
            var peers = _networkService.GetPeers(false)
                .Where(p => p.SyncState == SyncState.Finished && 
                            p.LastKnownLibHeight >= peerLibHeight &&
                            (exceptedPeers.IsNullOrEmpty() || !exceptedPeers.Contains(p.Pubkey)))
                .ToList();

            var randomPeerPubkey = peers.Count == 0
                ? defaultPeerPubkey
                : peers[random.Next() % peers.Count].Pubkey;

            return randomPeerPubkey;
        }

        private async Task<DownloadBlocksResult> DownloadBlocksAsync(string peerPubkey, DownloadBlockDto downloadBlockDto)
        {
            var downloadBlockCount = 0;
            var lastDownloadBlockHash = downloadBlockDto.PreviousBlockHash;
            var lastDownloadBlockHeight = downloadBlockDto.PreviousBlockHeight;

            Logger.LogDebug(
                $"Download blocks start with block height: {lastDownloadBlockHeight}, hash: {lastDownloadBlockHash}, PeerPubkey: {peerPubkey}");

            while (downloadBlockCount < downloadBlockDto.MaxBlockDownloadCount)
            {
                var getBlocksResult = await _networkService.GetBlocksAsync(lastDownloadBlockHash,
                    downloadBlockDto.BatchRequestBlockCount, peerPubkey);
                if (!getBlocksResult.Success)
                {
                    return new DownloadBlocksResult
                    {
                        Success = false
                    };
                }

                var blocksWithTransactions = getBlocksResult.Payload;
                if (blocksWithTransactions == null || !blocksWithTransactions.Any())
                {
                    Logger.LogWarning("No blocks returned.");
                    break;
                }

                foreach (var blockWithTransactions in blocksWithTransactions)
                {
                    Logger.LogDebug($"Processing block {blockWithTransactions}.");

                    if (blockWithTransactions.Height != lastDownloadBlockHeight + 1 ||
                        blockWithTransactions.Header.PreviousBlockHash != lastDownloadBlockHash)
                    {
                        Logger.LogWarning(
                            $"Received invalid block, peer: {peerPubkey}, block hash: {blockWithTransactions.GetHash()}, block height: {blockWithTransactions.Height}");
                        throw new BlockDownloadException(blockWithTransactions.GetHash(), blockWithTransactions.Height,
                            peerPubkey);
                    }

                    lastDownloadBlockHash = blockWithTransactions.GetHash();
                    lastDownloadBlockHeight = blockWithTransactions.Height;

                    EnqueueAttachBlockJob(blockWithTransactions, peerPubkey);
                    downloadBlockCount++;
                }

                var lastBlock = blocksWithTransactions.Last();
                lastDownloadBlockHash = lastBlock.GetHash();
                lastDownloadBlockHeight = lastBlock.Height;
            }

            if (downloadBlockCount != 0)
            {
                _blockSyncStateProvider.SetDownloadJobTargetState(lastDownloadBlockHash, false);
                _blockSyncStateProvider.LastRequestPeerPubkey = peerPubkey;
            }

            return new DownloadBlocksResult
            {
                Success = true,
                DownloadBlockCount = downloadBlockCount,
                LastDownloadBlockHash = lastDownloadBlockHash,
                LastDownloadBlockHeight = lastDownloadBlockHeight
            };
        }

        private void EnqueueAttachBlockJob(BlockWithTransactions blockWithTransactions, string senderPubkey)
        {
            _blockSyncQueueService.Enqueue(
                async () =>
                {
                    await _blockSyncAttachService.AttachBlockWithTransactionsAsync(blockWithTransactions, senderPubkey,
                        () =>
                        {
                            _blockSyncStateProvider.TryUpdateDownloadJobTargetState(
                                blockWithTransactions.GetHash(), true);
                            return Task.CompletedTask;
                        });
                },
                OSConstants.BlockSyncAttachQueueName);
        }

        public bool ValidateQueueAvailabilityBeforeDownload()
        {
            if (!_blockSyncQueueService.ValidateQueueAvailability(OSConstants.BlockSyncAttachQueueName))
            {
                Logger.LogWarning("Block sync attach queue is too busy.");
                return false;
            }

            if (!_blockSyncQueueService.ValidateQueueAvailability(KernelConstants.UpdateChainQueueName))
            {
                Logger.LogWarning("Block sync attach and execute queue is too busy.");
                return false;
            }

            return true;
        }

        public void RemoveDownloadJobTargetState(Hash targetBlockHash)
        {
            if (targetBlockHash != null)
                _blockSyncStateProvider.TryRemoveDownloadJobTargetState(targetBlockHash);
        }

        public bool IsNotReachedDownloadTarget(Hash targetBlockHash)
        {
            return _blockSyncStateProvider.TryGetDownloadJobTargetState(targetBlockHash, out var state)
                   && state == false;
        }
    }
}