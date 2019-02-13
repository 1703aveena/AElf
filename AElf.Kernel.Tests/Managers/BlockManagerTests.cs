﻿using System.ComponentModel;
using AElf.Kernel.Managers;
using System.Threading.Tasks;
using AElf.Common;
using Xunit;

namespace AElf.Kernel.Tests.Managers
{
    public sealed class BlockManagerTests:AElfKernelTestBase
    {
        private IBlockManager _blockManager;
        private int _chainId = 1234;

        public BlockManagerTests()
        {
            _blockManager = GetRequiredService<IBlockManager>();
        }

        [Fact]
        public async Task AddBlockHeaderTest()
        {
            var header = new BlockHeader();
            await _blockManager.AddBlockHeaderAsync(header);
        }

        [Fact]
        public async Task AddBlockBodyTest()
        {
            var hash = Hash.Generate();
            var body = new BlockBody();
            await _blockManager.AddBlockBodyAsync(hash, body);
        }

        [Fact]
        public async Task GetBlockHeaderTest()
        {
            var header = new BlockHeader()
            {
                ChainId = _chainId,
                Height = 1
            };
            var hash = header.GetHash();
            await _blockManager.AddBlockHeaderAsync(header);

            var h = await _blockManager.GetBlockHeaderAsync(hash);
            Assert.Equal(header, h);
        }

        [Fact]
        public async Task GetBlockBodyTest()
        {
            var header = new BlockHeader()
            {
                ChainId = _chainId,
                Height = 1
            };
            await _blockManager.AddBlockHeaderAsync(header);
            var hash = header.GetHash();
            var body = new BlockBody()
            {
                BlockHeader = hash
            };
            await _blockManager.AddBlockBodyAsync(hash, body);

            var block = await _blockManager.GetBlockAsync(hash);
            Assert.Equal(block.Header, header);
            Assert.Equal(block.Body, body);
        }

    }
}