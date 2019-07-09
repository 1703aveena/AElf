using System.Threading.Tasks;
using AElf.Kernel;
using AElf.Kernel.Blockchain.Application;
using AElf.OS.BlockSync.Infrastructure;
using AElf.Sdk.CSharp;
using AElf.Types;
using Shouldly;
using Xunit;

namespace AElf.OS.BlockSync.Application
{
    public class BlockSyncValidationServiceTests : BlockSyncTestBase
    {
        private readonly IBlockSyncValidationService _blockSyncValidationService;
        private readonly IBlockchainService _blockchainService;
        private readonly IBlockSyncStateProvider _blockSyncStateProvider;
        private readonly IAnnouncementCacheProvider _announcementCacheProvider;
        private readonly OSTestHelper _osTestHelper;

        public BlockSyncValidationServiceTests()
        {
            _blockSyncValidationService = GetRequiredService<IBlockSyncValidationService>();
            _blockchainService = GetRequiredService<IBlockchainService>();
            _blockSyncStateProvider = GetRequiredService<IBlockSyncStateProvider>();
            _announcementCacheProvider = GetRequiredService<IAnnouncementCacheProvider>();
            _osTestHelper = GetRequiredService<OSTestHelper>();
        }

        [Fact]
        public async Task ValidateBeforeHandleAnnounceAsync_Success()
        {
            var chain = await _blockchainService.GetChainAsync();

            var syncBlockHash = Hash.FromString("SyncBlockHash");
            var syncBlockHeight = chain.LastIrreversibleBlockHeight + 1;

            var validateResult =
                await _blockSyncValidationService.ValidateBeforeHandleAnnounceAsync(chain, syncBlockHash,
                    syncBlockHeight);

            validateResult.ShouldBeTrue();
        }

        [Fact]
        public async Task ValidateBeforeHandleAnnounceAsync_FetchQueueIsBusy()
        {
            var chain = await _blockchainService.GetChainAsync();

            var syncBlockHash = Hash.FromString("SyncBlockHash");
            var syncBlockHeight = chain.LastIrreversibleBlockHeight + 1;

            _blockSyncStateProvider.BlockSyncFetchBlockEnqueueTime = TimestampHelper.GetUtcNow()
                .AddMilliseconds(-(BlockSyncConstants.BlockSyncFetchBlockAgeLimit + 100));

            var validateResult =
                await _blockSyncValidationService.ValidateBeforeHandleAnnounceAsync(chain, syncBlockHash,
                    syncBlockHeight);

            validateResult.ShouldBeFalse();
        }

        [Fact]
        public async Task ValidateBeforeHandleAnnounceAsync_DownloadQueueIsBusy()
        {
            var chain = await _blockchainService.GetChainAsync();

            var syncBlockHash = Hash.FromString("SyncBlockHash");
            var syncBlockHeight = chain.LastIrreversibleBlockHeight + 1;

            _blockSyncStateProvider.BlockSyncDownloadBlockEnqueueTime = TimestampHelper.GetUtcNow()
                .AddMilliseconds(-(BlockSyncConstants.BlockSyncDownloadBlockAgeLimit + 100));

            var validateResult =
                await _blockSyncValidationService.ValidateBeforeHandleAnnounceAsync(chain, syncBlockHash,
                    syncBlockHeight);

            validateResult.ShouldBeFalse();
        }

        [Fact]
        public async Task ValidateBeforeHandleAnnounceAsync_AlreadySynchronized()
        {
            var chain = await _blockchainService.GetChainAsync();

            var syncBlockHash = Hash.FromString("SyncBlockHash");
            var syncBlockHeight = chain.LastIrreversibleBlockHeight - 1;

            _announcementCacheProvider.TryAddAnnouncementCache(syncBlockHash, syncBlockHeight);

            var validateResult =
                await _blockSyncValidationService.ValidateBeforeHandleAnnounceAsync(chain, syncBlockHash,
                    syncBlockHeight);

            validateResult.ShouldBeFalse();
        }

        [Fact]
        public async Task ValidateBeforeHandleAnnounceAsync_LessThenLIBHeight()
        {
            var chain = await _blockchainService.GetChainAsync();

            var syncBlockHash = Hash.FromString("SyncBlockHash");
            var syncBlockHeight = chain.LastIrreversibleBlockHeight - 1;

            var validateResult =
                await _blockSyncValidationService.ValidateBeforeHandleAnnounceAsync(chain, syncBlockHash,
                    syncBlockHeight);

            validateResult.ShouldBeFalse();
        }

        [Fact]
        public async Task ValidateBeforeHandleBlock_Success()
        {
            var chain = await _blockchainService.GetChainAsync();

            var block = _osTestHelper.GenerateBlockWithTransactions(chain.LastIrreversibleBlockHash,
                chain.LastIrreversibleBlockHeight);

            var validateResult = await _blockSyncValidationService.ValidateBeforeHandleBlockAsync(chain, block);

            validateResult.ShouldBeTrue();
        }

        [Fact]
        public async Task ValidateBeforeHandleBlock_FetchQueueIsBusy()
        {
            var chain = await _blockchainService.GetChainAsync();

            var block = _osTestHelper.GenerateBlockWithTransactions(chain.LastIrreversibleBlockHash,
                chain.LastIrreversibleBlockHeight);;

            _blockSyncStateProvider.BlockSyncFetchBlockEnqueueTime = TimestampHelper.GetUtcNow()
                .AddMilliseconds(-(BlockSyncConstants.BlockSyncFetchBlockAgeLimit + 100));

            var validateResult = await _blockSyncValidationService.ValidateBeforeHandleBlockAsync(chain, block);

            validateResult.ShouldBeFalse();
        }

        [Fact]
        public async Task ValidateBeforeHandleBlock_AlreadySynchronized()
        {
            var chain = await _blockchainService.GetChainAsync();

            var block = _osTestHelper.GenerateBlockWithTransactions(chain.LastIrreversibleBlockHash,
                chain.LastIrreversibleBlockHeight);

            _announcementCacheProvider.TryAddAnnouncementCache(block.GetHash(), block.Height);

            var validateResult = await _blockSyncValidationService.ValidateBeforeHandleBlockAsync(chain, block);

            validateResult.ShouldBeFalse();
        }
        
        [Fact]
        public async Task ValidateBeforeHandleBlock_LessThenLIBHeight()
        {
            var chain = await _blockchainService.GetChainAsync();

            var block = _osTestHelper.GenerateBlockWithTransactions(Hash.FromString("SyncBlockHash"),
                chain.LastIrreversibleBlockHeight - 1);

            var validateResult = await _blockSyncValidationService.ValidateBeforeHandleBlockAsync(chain, block);

            validateResult.ShouldBeFalse();
        }
    }
}