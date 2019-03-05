using System.Threading.Tasks;
using AElf.Kernel.Blockchain.Application;
using Google.Protobuf;

namespace AElf.Kernel.Consensus.Application
{
    public class ConsensusExtraDataProvider : IBlockExtraDataProvider
    {
        private readonly IConsensusService _consensusService;

        public ConsensusExtraDataProvider(IConsensusService consensusService)
        {
            _consensusService = consensusService;
        }

        public async Task FillExtraDataAsync(Block block)
        {
            if (block.Header.BlockExtraData == null)
            {
                block.Header.BlockExtraData = new BlockExtraData();
            }

            if (block.Height == 1 || !block.Header.BlockExtraData.ConsensusInformation.IsEmpty)
            {
                return;
            }

            var consensusInformation = await _consensusService.GetNewConsensusInformationAsync();

            if (consensusInformation == null)
            {
                return;
            }

            block.Header.BlockExtraData.ConsensusInformation = ByteString.CopyFrom(consensusInformation);
        }

        public async Task<bool> ValidateExtraDataAsync(Block block)
        {
            var consensusInformation = block.Header.BlockExtraData.ConsensusInformation;

            return await _consensusService.ValidateConsensusAsync(block.GetHash(), block.Height,
                consensusInformation.ToByteArray());
        }
    }
}