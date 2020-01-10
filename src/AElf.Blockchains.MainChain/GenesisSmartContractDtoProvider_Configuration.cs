using System.Collections.Generic;
using System.Linq;
using Acs0;
using AElf.Contracts.Configuration;
using AElf.Kernel;
using AElf.Kernel.SmartContract;
using AElf.OS.Node.Application;

namespace AElf.Blockchains.MainChain
{
    public partial class GenesisSmartContractDtoProvider
    {
        private IEnumerable<GenesisSmartContractDto> GetGenesisSmartContractDtosForConfiguration()
        {
            var l = new List<GenesisSmartContractDto>();
            l.AddGenesisSmartContract(_codes.Single(kv => kv.Key.Contains("Configuration")).Value,
                ConfigurationSmartContractAddressNameProvider.Name, GenerateConfigurationInitializationCallList());
            return l;
        }

        private SystemContractDeploymentInput.Types.SystemTransactionMethodCallList
            GenerateConfigurationInitializationCallList()
        {
            var crossChainMethodCallList = new SystemContractDeploymentInput.Types.SystemTransactionMethodCallList();
            crossChainMethodCallList.Add(
                nameof(ConfigurationContainer.ConfigurationStub.SetRequiredAcsInContracts),
                new RequiredAcsInContracts
                {
                    AcsList = SmartContractConstants.ContractFeeStrategyAcsList
                });
            return crossChainMethodCallList;
        }
    }
}
