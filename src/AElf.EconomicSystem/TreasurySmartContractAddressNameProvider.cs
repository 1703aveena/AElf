using AElf.Kernel.SmartContract;
using AElf.Types;
using Volo.Abp.DependencyInjection;

namespace AElf.EconomicSystem
{
    public class TreasurySmartContractAddressNameProvider : ISmartContractAddressNameProvider, ISingletonDependency
    {
        public static Hash Name = HashHelper.ComputeFromString("AElf.ContractNames.Treasury");

        public Hash ContractName => Name;
    }
}