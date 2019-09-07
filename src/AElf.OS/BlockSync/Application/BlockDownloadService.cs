using System;
using System.Linq;
using System.Threading.Tasks;
using AElf.Kernel;
using AElf.OS.BlockSync.Dto;
using AElf.OS.BlockSync.Infrastructure;
using AElf.OS.BlockSync.Types;
using AElf.OS.Network;
using AElf.OS.Network.Application;
using AElf.Types;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AElf.OS.BlockSync.Application
{
    public class BlockDownloadService : IBlockDownloadService
    {
        private readonly INetworkService _networkService;
        private readonly IBlockSyncAttachService _blockSyncAttachService;
        private readonly IBlockSyncQueueService _blockSyncQueueService;
        private readonly IBlockSyncStateProvider _blockSyncStateProvider;

        public ILogger<BlockDownloadService> Logger { get; set; }

        public BlockDownloadService(INetworkService networkService,
            IBlockSyncAttachService blockSyncAttachService,
            IBlockSyncQueueService blockSyncQueueService,
            IBlockSyncStateProvider blockSyncStateProvider)
        {
            Logger = NullLogger<BlockDownloadService>.Instance;

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
            if (downloadBlockDto.UseSuggestedPeer)
            {
                return await DownloadBlocksAsync(downloadBlockDto, downloadBlockDto.SuggestedPeerPubkey);
            }

            var suggestedPeer = _networkService.GetPeerByPubkey(downloadBlockDto.SuggestedPeerPubkey);
            var downloadTargetHeight = downloadBlockDto.PreviousBlockHeight + downloadBlockDto.MaxBlockDownloadCount;
            if (downloadTargetHeight > suggestedPeer.LastKnownLibHeight)
            {
                return await DownloadBlocksAsync(downloadBlockDto, downloadBlockDto.SuggestedPeerPubkey);
            }

            var random = new Random();
            var peers = _networkService.GetPeers()
                .Where(p => p.LastKnownLibHeight >= downloadTargetHeight &&
                            p.Pubkey != _blockSyncStateProvider.LastRequestPeerPubkey)
                .ToList();

            var randomPeer = peers.Count == 0 ? suggestedPeer : peers[random.Next() % peers.Count];

            var downloadResult = await DownloadBlocksAsync(downloadBlockDto, randomPeer.Pubkey);
            if (downloadResult.DownloadBlockCount == 0)
            {
                // TODO: Handle bad peer or network problems.
                // If cannot get the blocks, there should be network problems or bad peer,
                // because we have selected peer with lib height greater than or equal to the target height.
                // 1. network problems, need to retry from other peer.
                // 2. not network problems, this peer or the last peer is bad peer, we need to remove it.
                //
                // But now we have no way to know if it is a network problem through the network service,
                // so we need to modify the implementation of NetworkService.GetBlocksAsync.
                Logger.LogWarning("Found bad peer or network problems.");
            }

            return downloadResult;
        }

        private async Task<DownloadBlocksResult> DownloadBlocksAsync(DownloadBlockDto downloadBlockDto,
            string peerPubkey)
        {
            var downloadBlockCount = 0;
            var lastDownloadBlockHash = downloadBlockDto.PreviousBlockHash;
            var lastDownloadBlockHeight = downloadBlockDto.PreviousBlockHeight;

            Logger.LogDebug(
                $"Download blocks start with block hash: {lastDownloadBlockHash}, block height: {lastDownloadBlockHeight}, PeerPubkey: {peerPubkey}");

            _blockSyncStateProvider.LastRequestPeerPubkey = peerPubkey;
            while (downloadBlockCount < downloadBlockDto.MaxBlockDownloadCount)
            {
                var blocksWithTransactions = await _networkService.GetBlocksAsync(lastDownloadBlockHash,
                    downloadBlockDto.BatchRequestBlockCount, peerPubkey);

                if (blocksWithTransactions == null || !blocksWithTransactions.Any())
                {
                    Logger.LogWarning("No blocks returned.");
                    break;
                }

                if (blocksWithTransactions.First().Header.PreviousBlockHash != lastDownloadBlockHash)
                {
                    throw new InvalidOperationException(
                        $"Previous block not match previous {lastDownloadBlockHash}, network back {blocksWithTransactions.First().Header.PreviousBlockHash}");
                }

                foreach (var blockWithTransactions in blocksWithTransactions)
                {
                    Logger.LogDebug($"Processing block {blockWithTransactions}.");
                    EnqueueAttachBlockJob(blockWithTransactions);
                    downloadBlockCount++;
                }

                var lastBlock = blocksWithTransactions.Last();
                lastDownloadBlockHash = lastBlock.GetHash();
                lastDownloadBlockHeight = lastBlock.Height;
            }

            if (lastDownloadBlockHash != null)
                _blockSyncStateProvider.SetDownloadJobTargetState(lastDownloadBlockHash, false);

            return new DownloadBlocksResult
            {
                DownloadBlockCount = downloadBlockCount,
                LastDownloadBlockHash = lastDownloadBlockHash,
                LastDownloadBlockHeight = lastDownloadBlockHeight
            };
        }

        private void EnqueueAttachBlockJob(BlockWithTransactions blockWithTransactions)
        {
            _blockSyncQueueService.Enqueue(
                async () =>
                {
                    await _blockSyncAttachService.AttachBlockWithTransactionsAsync(blockWithTransactions,
                         () =>
                        {
                            _blockSyncStateProvider.TryUpdateDownloadJobTargetState(blockWithTransactions.GetHash(),
                                true);
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