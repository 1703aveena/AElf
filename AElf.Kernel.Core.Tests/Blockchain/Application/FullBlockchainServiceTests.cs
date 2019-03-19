using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf.Common;
using AElf.Kernel.Blockchain.Domain;
using AElf.Kernel.Blockchain.Events;
using Google.Protobuf.WellKnownTypes;
using Shouldly;
using Shouldly.ShouldlyExtensionMethods;
using Volo.Abp.EventBus.Local;
using Xunit;

namespace AElf.Kernel.Blockchain.Application
{
    public class FullBlockchainServiceTests : AElfKernelTestBase_Temp
    {
        private readonly FullBlockchainService _fullBlockchainService;
        private readonly ITransactionManager _transactionManager;
        private readonly KernelTestHelper _kernelTestHelper;

        public FullBlockchainServiceTests()
        {
            _fullBlockchainService = GetRequiredService<FullBlockchainService>();
            _transactionManager = GetRequiredService<ITransactionManager>();
            _kernelTestHelper = GetRequiredService<KernelTestHelper>();
        }

        [Fact]
        public async Task Add_Block_Success()
        {
            var block = new Block
            {
                Height = 2,
                Header = new BlockHeader(),
                Body = new BlockBody()
            };
            for (var i = 0; i < 3; i++)
            {
                block.Body.AddTransaction(_kernelTestHelper.GenerateEmptyTransaction());
            }

            var existBlock = await _fullBlockchainService.GetBlockByHashAsync(block.GetHash());
            existBlock.ShouldBeNull();

            await _fullBlockchainService.AddBlockAsync(block);

            existBlock = await _fullBlockchainService.GetBlockByHashAsync(block.GetHash());
            existBlock.GetHash().ShouldBe(block.GetHash());
            existBlock.Body.TransactionsCount.ShouldBe(3);

            foreach (var tx in block.Body.TransactionList)
            {
                var existTransaction = await _transactionManager.GetTransaction(tx.GetHash());
                existTransaction.ShouldBe(tx);
            }
        }

        [Fact]
        public async Task Has_Block_ReturnTrue()
        {
            var result = await _fullBlockchainService.HasBlockAsync(_kernelTestHelper.BestBranchBlockList[1].GetHash());
            result.ShouldBeTrue();

            result = await _fullBlockchainService.HasBlockAsync(_kernelTestHelper.LongestBranchBlockList[1].GetHash());
            result.ShouldBeTrue();

            result = await _fullBlockchainService.HasBlockAsync(_kernelTestHelper.ForkBranchBlockList[1].GetHash());
            result.ShouldBeTrue();

            result = await _fullBlockchainService.HasBlockAsync(_kernelTestHelper.UnlinkedBranchBlockList[1].GetHash());
            result.ShouldBeTrue();
        }

        [Fact]
        public async Task Has_Block_ReturnFalse()
        {
            var result = await _fullBlockchainService.HasBlockAsync(Hash.FromString("Not Exist Block"));
            result.ShouldBeFalse();
        }

        [Fact]
        public async Task Get_BlockHash_ByHeight_ReturnNull()
        {
            var result =
                await _fullBlockchainService.GetBlockHashByHeightAsync(_kernelTestHelper.Chain, 14,
                    _kernelTestHelper.Chain.BestChainHash);
            result.ShouldBeNull();

            result = await _fullBlockchainService.GetBlockHashByHeightAsync(_kernelTestHelper.Chain, 14,
                _kernelTestHelper.Chain.LongestChainHash);
            result.ShouldBeNull();

            result = await _fullBlockchainService.GetBlockHashByHeightAsync(_kernelTestHelper.Chain, 14,
                _kernelTestHelper.ForkBranchBlockList.Last().GetHash());
            result.ShouldBeNull();

            result = await _fullBlockchainService.GetBlockHashByHeightAsync(_kernelTestHelper.Chain, 15,
                _kernelTestHelper.UnlinkedBranchBlockList.Last().GetHash());
            result.ShouldBeNull();
        }

        [Fact]
        public async Task Get_BlockHash_ByHeight_ReturnHash()
        {
            var result = await _fullBlockchainService.GetBlockHashByHeightAsync(_kernelTestHelper.Chain,
                _kernelTestHelper.BestBranchBlockList[8].Height, _kernelTestHelper.Chain.BestChainHash);
            result.ShouldBe(_kernelTestHelper.BestBranchBlockList[8].GetHash());

            result = await _fullBlockchainService.GetBlockHashByHeightAsync(_kernelTestHelper.Chain, _kernelTestHelper
                .LongestBranchBlockList[3].Height, _kernelTestHelper.Chain.LongestChainHash);
            result.ShouldBe(_kernelTestHelper.LongestBranchBlockList[3].GetHash());

            result = await _fullBlockchainService.GetBlockHashByHeightAsync(_kernelTestHelper.Chain, _kernelTestHelper
                .ForkBranchBlockList[3].Height, _kernelTestHelper.ForkBranchBlockList.Last().GetHash());
            result.ShouldBe(_kernelTestHelper.ForkBranchBlockList[3].GetHash());

            // search irreversible section of the chain
            result = await _fullBlockchainService.GetBlockHashByHeightAsync(_kernelTestHelper.Chain,
                _kernelTestHelper.Chain.LastIrreversibleBlockHeight - 1, _kernelTestHelper.Chain.BestChainHash);
            result.ShouldBe(_kernelTestHelper.BestBranchBlockList[3].GetHash());
        }

        [Fact]
        public async Task Set_BestChain_Success()
        {
            _kernelTestHelper.Chain.BestChainHeight.ShouldBe(_kernelTestHelper.BestBranchBlockList.Last().Height);
            _kernelTestHelper.Chain.BestChainHash.ShouldBe(_kernelTestHelper.BestBranchBlockList.Last().GetHash());

            await _fullBlockchainService.SetBestChainAsync(_kernelTestHelper.Chain,
                _kernelTestHelper.Chain.LongestChainHeight,
                _kernelTestHelper.Chain.LongestChainHash);

            var chain = await _fullBlockchainService.GetChainAsync();
            chain.BestChainHeight.ShouldBe(_kernelTestHelper.LongestBranchBlockList.Last().Height);
            chain.BestChainHash.ShouldBe(_kernelTestHelper.LongestBranchBlockList.Last().GetHash());
        }

        [Fact]
        public async Task Get_GetReservedBlockHashes_ReturnNull()
        {
            var result = await _fullBlockchainService.GetReversedBlockHashes(Hash.FromString("not exist"), 1);
            result.ShouldBeNull();

            result = await _fullBlockchainService.GetReversedBlockHashes(_kernelTestHelper.Chain.GenesisBlockHash, 1);
            result.ShouldBeNull();
        }

        [Fact]
        public async Task Get_ReversedBlockHashes_ReturnEmpty()
        {
            var result =
                await _fullBlockchainService.GetReversedBlockHashes(_kernelTestHelper.BestBranchBlockList[2].GetHash(),
                    0);
            result.Count.ShouldBe(0);
        }

        [Fact]
        public async Task Get_GetReservedBlockHashes_ReturnHashes()
        {
            var result =
                await _fullBlockchainService.GetReversedBlockHashes(_kernelTestHelper.BestBranchBlockList[5].GetHash(), 3);
            result.Count.ShouldBe(3);
            result[0].ShouldBe(_kernelTestHelper.BestBranchBlockList[4].GetHash());
            result[1].ShouldBe(_kernelTestHelper.BestBranchBlockList[3].GetHash());
            result[2].ShouldBe(_kernelTestHelper.BestBranchBlockList[2].GetHash());

            result = await _fullBlockchainService.GetReversedBlockHashes(_kernelTestHelper.BestBranchBlockList[3].GetHash(), 4);
            result.Count.ShouldBe(3);
            result[0].ShouldBe(_kernelTestHelper.BestBranchBlockList[2].GetHash());
            result[1].ShouldBe(_kernelTestHelper.BestBranchBlockList[1].GetHash());
            result[2].ShouldBe(_kernelTestHelper.Chain.GenesisBlockHash);
        }

        [Fact]
        public async Task Get_Blocks_ReturnEmpty()
        {
            var result = await _fullBlockchainService.GetBlocksInBestChainBranchAsync(Hash.FromString("not exist"), 3);
            result.Count.ShouldBe(0);
        }

        [Fact]
        public async Task Get_Blocks_ReturnBlocks()
        {
            var result = await _fullBlockchainService.GetBlocksInBestChainBranchAsync(_kernelTestHelper.Chain.BestChainHash, 3);
            result.Count.ShouldBe(0);

            result = await _fullBlockchainService.GetBlocksInBestChainBranchAsync(
                _kernelTestHelper.BestBranchBlockList[3].GetHash(), 3);
            result.Count.ShouldBe(3);
            result[0].GetHash().ShouldBe(_kernelTestHelper.BestBranchBlockList[4].GetHash());
            result[1].GetHash().ShouldBe(_kernelTestHelper.BestBranchBlockList[5].GetHash());
            result[2].GetHash().ShouldBe(_kernelTestHelper.BestBranchBlockList[6].GetHash());

            result = await _fullBlockchainService.GetBlocksInBestChainBranchAsync(
                _kernelTestHelper.BestBranchBlockList[8].GetHash(), 3);
            result.Count.ShouldBe(2);
            result[0].GetHash().ShouldBe(_kernelTestHelper.BestBranchBlockList[9].GetHash());
            result[1].GetHash().ShouldBe(_kernelTestHelper.BestBranchBlockList[10].GetHash());

            result = await _fullBlockchainService.GetBlocksInBestChainBranchAsync(
                _kernelTestHelper.LongestBranchBlockList[0].GetHash(), 3);
            result.Count.ShouldBe(0);
        }

        [Fact]
        public async Task Get_GetBlockHashes_ReturnEmpty()
        {
            var notExistHash = Hash.FromString("not exist");

            var result = await _fullBlockchainService.GetBlockHashesAsync(_kernelTestHelper.Chain, notExistHash, 1,
                _kernelTestHelper.Chain.BestChainHash);
            result.Count.ShouldBe(0);

            result = await _fullBlockchainService.GetBlockHashesAsync(_kernelTestHelper.Chain, notExistHash, 1,
                _kernelTestHelper.Chain.LongestChainHash);
            result.Count.ShouldBe(0);

            result = await _fullBlockchainService.GetBlockHashesAsync(_kernelTestHelper.Chain, notExistHash, 1,
                _kernelTestHelper.ForkBranchBlockList.Last().GetHash());
            result.Count.ShouldBe(0);

            await _fullBlockchainService
                .GetBlockHashesAsync(_kernelTestHelper.Chain, _kernelTestHelper.Chain.BestChainHash, 10,
                    _kernelTestHelper.Chain.BestChainHash)
                .ContinueWith(p => p.Result.Count.ShouldBe(0));

            await _fullBlockchainService
                .GetBlockHashesAsync(_kernelTestHelper.Chain, _kernelTestHelper.Chain.LongestChainHash, 10,
                    _kernelTestHelper.Chain.LongestChainHash)
                .ContinueWith(p => p.Result.Count.ShouldBe(0));

            await _fullBlockchainService
                .GetBlockHashesAsync(_kernelTestHelper.Chain, _kernelTestHelper.ForkBranchBlockList.Last().GetHash(),
                    10,
                    _kernelTestHelper.ForkBranchBlockList.Last().GetHash())
                .ContinueWith(p => p.Result.Count.ShouldBe(0));
        }

        [Fact]
        public async Task Get_GetBlockHashes_ThrowInvalidOperationException()
        {
            await _fullBlockchainService
                .GetBlockHashesAsync(_kernelTestHelper.Chain, _kernelTestHelper.BestBranchBlockList[5].GetHash(), 2,
                    _kernelTestHelper.UnlinkedBranchBlockList.Last().GetHash()).ShouldThrowAsync<InvalidOperationException>();
        }

        [Fact]
        public async Task Get_GetBlockHashes_ReturnHashes()
        {
            var result = await _fullBlockchainService.GetBlockHashesAsync(_kernelTestHelper.Chain,
                _kernelTestHelper.BestBranchBlockList[1].GetHash(), 3, _kernelTestHelper.BestBranchBlockList[6].GetHash());
            result.Count.ShouldBe(3);
            result[0].ShouldBe(_kernelTestHelper.BestBranchBlockList[2].GetHash());
            result[1].ShouldBe(_kernelTestHelper.BestBranchBlockList[3].GetHash());
            result[2].ShouldBe(_kernelTestHelper.BestBranchBlockList[4].GetHash());
            
            result = await _fullBlockchainService.GetBlockHashesAsync(_kernelTestHelper.Chain,
                _kernelTestHelper.BestBranchBlockList[7].GetHash(), 10, _kernelTestHelper.BestBranchBlockList[9].GetHash());
            result.Count.ShouldBe(2);
            result[0].ShouldBe(_kernelTestHelper.BestBranchBlockList[8].GetHash());
            result[1].ShouldBe(_kernelTestHelper.BestBranchBlockList[9].GetHash());
            
            result = await _fullBlockchainService.GetBlockHashesAsync(_kernelTestHelper.Chain,
                _kernelTestHelper.LongestBranchBlockList[0].GetHash(), 2, _kernelTestHelper.LongestBranchBlockList[3].GetHash());
            result.Count.ShouldBe(2);
            result[0].ShouldBe(_kernelTestHelper.LongestBranchBlockList[1].GetHash());
            result[1].ShouldBe(_kernelTestHelper.LongestBranchBlockList[2].GetHash());
            
            result = await _fullBlockchainService.GetBlockHashesAsync(_kernelTestHelper.Chain,
                _kernelTestHelper.LongestBranchBlockList[0].GetHash(), 10, _kernelTestHelper.LongestBranchBlockList[3].GetHash());
            result.Count.ShouldBe(3);
            result[0].ShouldBe(_kernelTestHelper.LongestBranchBlockList[1].GetHash());
            result[1].ShouldBe(_kernelTestHelper.LongestBranchBlockList[2].GetHash());
            result[2].ShouldBe(_kernelTestHelper.LongestBranchBlockList[3].GetHash());
        }

        [Fact]
        public async Task Get_Block_ByHeight_ReturnBlock()
        {
            var result =
                await _fullBlockchainService.GetBlockByHeightInBestChainBranchAsync(
                    _kernelTestHelper.BestBranchBlockList[3].Height);
            result.ShouldNotBeNull();
            result.GetHash().ShouldBe(_kernelTestHelper.BestBranchBlockList[3].GetHash());
        }

        [Fact]
        public async Task Get_Block_ByHeight_ReturnNull()
        {
            var result = await _fullBlockchainService.GetBlockByHeightInBestChainBranchAsync(15);
            result.ShouldBeNull();
        }

        [Fact]
        public async Task Get_Block_ByHash_ReturnBlock()
        {
            var block = new Block
            {
                Height = 12,
                Header = new BlockHeader(),
                Body = new BlockBody()
            };
            for (var i = 0; i < 3; i++)
            {
                block.Body.AddTransaction(_kernelTestHelper.GenerateEmptyTransaction());
            }

            await _fullBlockchainService.AddBlockAsync(block);
            var result = await _fullBlockchainService.GetBlockByHashAsync(block.GetHash());
            result.GetHash().ShouldBe(block.GetHash());
            result.Body.TransactionList.Count.ShouldBe(block.Body.TransactionsCount);
            result.Body.Transactions[0].ShouldBe(block.Body.Transactions[0]);
            result.Body.Transactions[1].ShouldBe(block.Body.Transactions[1]);
            result.Body.Transactions[2].ShouldBe(block.Body.Transactions[2]);
        }

        [Fact]
        public async Task Get_Block_ByHash_ReturnNull()
        {
            var result = await _fullBlockchainService.GetBlockByHashAsync(Hash.FromString("Not Exist Block"));
            result.ShouldBeNull();
        }

        [Fact]
        public async Task Get_BlockHeaderByHash_ReturnHeader()
        {
            var blockHeader =
                await _fullBlockchainService.GetBlockHeaderByHeightAsync(_kernelTestHelper.BestBranchBlockList[2].Height);
            blockHeader.GetHash().ShouldBe(_kernelTestHelper.BestBranchBlockList[2].GetHash());
        }

        [Fact]
        public async Task Get_Chain_ReturnChain()
        {
            var chain = await _fullBlockchainService.GetChainAsync();
            chain.ShouldNotBeNull();
        }

        [Fact]
        public async Task Get_BestChain_ReturnBlockHeader()
        {
            var newBlock = new Block
            {
                Header = new BlockHeader
                {
                    Height = _kernelTestHelper.Chain.BestChainHeight + 1,
                    PreviousBlockHash = Hash.FromString("New Branch")
                },
                Body = new BlockBody()
            };

            await _fullBlockchainService.AddBlockAsync(newBlock);
            var chain = await _fullBlockchainService.GetChainAsync();
            await _fullBlockchainService.AttachBlockToChainAsync(chain, newBlock);

            var result = await _fullBlockchainService.GetBestChainLastBlockHeaderAsync();
            result.Height.ShouldBe(_kernelTestHelper.BestBranchBlockList.Last().Height);
            result.GetHash().ShouldBe(_kernelTestHelper.BestBranchBlockList.Last().GetHash());
        }
        
        [Fact]
        public async Task Get_BlocksInBestChainBranch_ReturnHashes()
        {
            var result =
                await _fullBlockchainService.GetBlocksInBestChainBranchAsync(_kernelTestHelper.BestBranchBlockList[0].GetHash(),
                    2);
            result.Count.ShouldBe(2);
            result[0].GetHash().ShouldBe(_kernelTestHelper.BestBranchBlockList[1].GetHash());
            result[1].GetHash().ShouldBe(_kernelTestHelper.BestBranchBlockList[2].GetHash());

            result = await _fullBlockchainService.GetBlocksInBestChainBranchAsync(
                _kernelTestHelper.BestBranchBlockList[7].GetHash(), 10);
            result.Count.ShouldBe(3);
            result[0].GetHash().ShouldBe(_kernelTestHelper.BestBranchBlockList[8].GetHash());
            result[1].GetHash().ShouldBe(_kernelTestHelper.BestBranchBlockList[9].GetHash());
            result[2].GetHash().ShouldBe(_kernelTestHelper.BestBranchBlockList[10].GetHash());
        }
        
        [Fact]
        public async Task Get_BlocksInLongestChainBranchAsync_ReturnHashes()
        {
            var result =
                await _fullBlockchainService.GetBlocksInLongestChainBranchAsync(
                    _kernelTestHelper.LongestBranchBlockList[0].GetHash(), 2);
            result.Count.ShouldBe(2);
            result[0].GetHash().ShouldBe(_kernelTestHelper.LongestBranchBlockList[1].GetHash());
            result[1].GetHash().ShouldBe(_kernelTestHelper.LongestBranchBlockList[2].GetHash());

            result = await _fullBlockchainService.GetBlocksInLongestChainBranchAsync(
                _kernelTestHelper.LongestBranchBlockList[0].GetHash(), 10);
            result.Count.ShouldBe(4);
            result[0].GetHash().ShouldBe(_kernelTestHelper.LongestBranchBlockList[1].GetHash());
            result[1].GetHash().ShouldBe(_kernelTestHelper.LongestBranchBlockList[2].GetHash());
            result[2].GetHash().ShouldBe(_kernelTestHelper.LongestBranchBlockList[3].GetHash());
            result[3].GetHash().ShouldBe(_kernelTestHelper.LongestBranchBlockList[4].GetHash());
        }
    }
}