using System;
using System.Threading.Tasks;
using AElf.Common;
using AElf.Kernel;
using AElf.Kernel.Blockchain.Application;
using AElf.OS.Network;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace AElf.OS.Jobs
{
    public class ForkDownloadJob : AsyncBackgroundJob<ForkDownloadJobArgs>
    {
        public IOptionsSnapshot<ChainOptions> ChainOptions { get; set; }

        public IFullBlockchainService BlockchainService { get; set; }
        public INetworkService NetworkService { get; set; }

        private int ChainId
        {
            get { return ChainOptions.Value.ChainId.ConvertBase58ToChainId(); }
        }

        public ForkDownloadJob()
        {
            Logger = NullLogger<ForkDownloadJob>.Instance;
        }

        protected override async Task ExecuteAsync(ForkDownloadJobArgs args)
        {
            try
            {
                Logger.LogDebug($"Starting download of {args.BlockHashes.Count} blocks from {args.Peer}.");

                var chain = await BlockchainService.GetChainAsync(ChainId);

                if (chain == null)
                {
                    Logger.LogError(
                        $"Failed to finish download of {args.BlockHashes.Count} blocks from {args.Peer}: chain not found.");
                }

                foreach (var hash in args.BlockHashes)
                {
                    // Check that some other job didn't get this before.
                    var hasBlock = await BlockchainService.HasBlockAsync(ChainId, hash);

                    if (hasBlock)
                        continue; // todo review maybe no need to go further.

                    // Query the peer
                    Block block = (Block) await NetworkService.GetBlockByHashAsync(hash, args.Peer);

                    // Add to our chain
                    await BlockchainService.AddBlockAsync(ChainId, block);
                    await BlockchainService.AttachBlockToChainAsync(chain, block);

                    Logger.LogDebug($"Added {block}.");
                }
            }
            catch (Exception e)
            {
                Logger.LogError(e, $"Failed to finish download job from {args.Peer}");
                throw;
            }
        }
    }
}

    