using System.Collections.Generic;
using System.Linq;
using AElf.Consensus.AElfConsensus;
using AElf.Contracts.Consensus.AElfConsensus;
using AElf.Kernel;
using AElf.Kernel.Consensus.AElfConsensus;
using AElf.OS.Node.Application;
using Google.Protobuf;

namespace AElf.Blockchains.MainChain
{
    public partial class GenesisSmartContractDtoProvider
    {
        public IEnumerable<GenesisSmartContractDto> GetGenesisSmartContractDtosForConsensus(Address zeroContractAddress)
        {
            var l = new List<GenesisSmartContractDto>();
            l.AddGenesisSmartContract<AElfConsensusContract>(ConsensusSmartContractAddressNameProvider.Name,
                GenerateConsensusInitializationCallList());
            return l;
        }

        private SystemContractDeploymentInput.Types.SystemTransactionMethodCallList
            GenerateConsensusInitializationCallList()
        {
            var aelfConsensusMethodCallList = new SystemContractDeploymentInput.Types.SystemTransactionMethodCallList();
            aelfConsensusMethodCallList.Add(nameof(AElfConsensusContract.InitialAElfConsensusContract),
                new InitialAElfConsensusContractInput
                {
                    ElectionContractSystemName = ElectionSmartContractAddressNameProvider.Name,
                    BaseTimeUnit = (int) TimeUnit.Minutes
                });
            aelfConsensusMethodCallList.Add(nameof(AElfConsensusContract.FirstRound),
                new Miners
                {
                    PublicKeys =
                    {
                        _consensusOptions.InitialMiners.Select(p =>
                            ByteString.CopyFrom(ByteArrayHelpers.FromHexString(p)))
                    }
                }.GenerateFirstRoundOfNewTerm(_consensusOptions.MiningInterval,
                    _consensusOptions.StartTimestamp.ToUniversalTime()));
            return aelfConsensusMethodCallList;
        }
    }
}