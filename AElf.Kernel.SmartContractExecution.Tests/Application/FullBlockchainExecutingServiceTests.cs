using System.Linq;
using System.Threading.Tasks;
using AElf.Common;
using AElf.Kernel.Blockchain.Application;
using AElf.Kernel.Blockchain.Events;
using Shouldly;
using Volo.Abp.EventBus.Local;
using Xunit;

namespace AElf.Kernel.SmartContractExecution.Application
{
    public sealed class FullBlockchainExecutingServiceTests : SmartContractExecutionTestBase
    {
        private readonly FullBlockchainExecutingService _fullBlockchainExecutingService;
        private readonly IFullBlockchainService _fullBlockchainService;
        private readonly ILocalEventBus _localEventBus;
        private readonly int _chainId = 1;

        public FullBlockchainExecutingServiceTests()
        {
            _fullBlockchainExecutingService = GetRequiredService<FullBlockchainExecutingService>();
            _fullBlockchainService = GetRequiredService<IFullBlockchainService>();
            _localEventBus = GetRequiredService<ILocalEventBus>();
        }

        [Fact]
        public async Task Attach_Block_To_Chain_ReturnNull()
        {
            var eventMessage = new BestChainFoundEventData();
            _localEventBus.Subscribe<BestChainFoundEventData>(message =>
            {
                eventMessage = message;
                return Task.CompletedTask;
            });

            var chain = await CreateNewChain();

            var newBlock = new Block
            {
                Header = new BlockHeader
                {
                    Height = 2,
                    PreviousBlockHash = Hash.Zero
                },
                Body = new BlockBody()
            };

            var status = await _fullBlockchainService.AttachBlockToChainAsync(chain, newBlock);

            var attachResult =
                await _fullBlockchainExecutingService.ExecuteBlocksAttachedToChain(chain, newBlock, status);
            attachResult.ShouldBeNull();
            eventMessage.BlockHeight.ShouldBe(ChainConsts.GenesisBlockHeight);
        }

        [Fact]
        public async Task Attach_Block_To_Chain_FoundBestChain()
        {
            var eventMessage = new BestChainFoundEventData();
            _localEventBus.Subscribe<BestChainFoundEventData>(message =>
            {
                eventMessage = message;
                return Task.CompletedTask;
            });

            var chain = await CreateNewChain();

            var newBlock = new Block
            {
                Header = new BlockHeader
                {
                    Height = chain.LongestChainHeight + 1,
                    PreviousBlockHash = chain.LongestChainHash
                },
                Body = new BlockBody()
            };

            await _fullBlockchainService.AddBlockAsync(chain.Id, newBlock);
            var status = await _fullBlockchainService.AttachBlockToChainAsync(chain, newBlock);
            var attachResult =
                await _fullBlockchainExecutingService.ExecuteBlocksAttachedToChain(chain, newBlock, status);
            attachResult.Count.ShouldBe(2);

            attachResult.Last().Height.ShouldBe(2u);
            
            

            //event was async, wait
            await Task.Delay(10);
            //TODO: fix the best chain not equal to height 2
            //eventMessage.BlockHeight.ShouldBe(newBlock.Header.Height);
        }

        private async Task<Chain> CreateNewChain()
        {
            var genesisBlock = new Block
            {
                Header = new BlockHeader
                {
                    Height = ChainConsts.GenesisBlockHeight,
                    PreviousBlockHash = Hash.Genesis
                },
                Body = new BlockBody()
            };
            var chain = await _fullBlockchainService.CreateChainAsync(_chainId, genesisBlock);
            return chain;
        }
    }
}