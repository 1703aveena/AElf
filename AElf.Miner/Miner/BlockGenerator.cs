using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf.ChainController;
using AElf.Common;
using AElf.Kernel;
using AElf.Types.CSharp;
using Google.Protobuf;

namespace AElf.Miner.Miner
{
    public class BlockGenerator
    {
        private readonly IChainService _chainService;
        private int ChainId { get; }

        public BlockGenerator(IChainService chainService, int chainId)
        {
            _chainService = chainService;
            ChainId = chainId;
        }

        public async Task<IBlock> GenerateBlockAsync(HashSet<TransactionResult> results, Hash sideChainTransactionsRoot,
            DateTime currentBlockTime)
        {
            var blockChain = _chainService.GetBlockChain(ChainId);

            var currentBlockHash = await blockChain.GetCurrentBlockHashAsync();
            var height = await blockChain.GetCurrentBlockHeightAsync() + 1;
            var block = new Block(currentBlockHash)
            {
                Header =
                {
                    Height = height,
                    ChainId = ChainId,
                    Bloom = ByteString.CopyFrom(
                        Bloom.AndMultipleBloomBytes(
                            results.Where(x => !x.Bloom.IsEmpty).Select(x => x.Bloom.ToByteArray())
                        )
                    ),
                    SideChainTransactionsRoot = sideChainTransactionsRoot
                }
            };

            // calculate and set tx merkle tree root 
            block.Complete(currentBlockTime, results);
            return block;
        }
    }
}