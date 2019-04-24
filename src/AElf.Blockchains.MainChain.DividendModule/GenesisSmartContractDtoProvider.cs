using System.Collections.Generic;
using AElf.Consensus.DPoS;
using AElf.Contracts.Dividend;
using AElf.Kernel;
using AElf.OS.Node.Application;

namespace AElf.Blockchains.MainChain.DividendModule
{
    public class GenesisSmartContractDtoProvider : IGenesisSmartContractDtoProvider
    {
        public IEnumerable<GenesisSmartContractDto> GetGenesisSmartContractDtos(Address zeroContractAddress)
        {
            var l = new List<GenesisSmartContractDto>();
            l.AddGenesisSmartContract<DividendContract>(
                DividendSmartContractAddressNameProvider.Name,
                GenerateDividendInitializationCallList());

            return l;
        }

        private SystemContractDeploymentInput.Types.SystemTransactionMethodCallList
            GenerateDividendInitializationCallList()
        {
            var dividendMethodCallList = new SystemContractDeploymentInput.Types.SystemTransactionMethodCallList();
            dividendMethodCallList.Add(nameof(DividendContract.InitializeDividendContract),
                new InitialDividendContractInput
                {
                    ConsensusContractSystemName = ConsensusSmartContractAddressNameProvider.Name,
                    TokenContractSystemName = TokenSmartContractAddressNameProvider.Name
                });
            return dividendMethodCallList;
        }
    }
}