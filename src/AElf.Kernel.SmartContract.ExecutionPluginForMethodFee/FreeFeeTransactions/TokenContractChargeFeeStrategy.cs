using System.Collections.Generic;
using AElf.Contracts.MultiToken;
using AElf.Kernel.SmartContract.Application;
using AElf.Kernel.Token;
using AElf.Types;

namespace AElf.Kernel.SmartContract.ExecutionPluginForMethodFee.FreeFeeTransactions
{
    public class TokenContractChargeFeeStrategy : IChargeFeeStrategy
    {
        private readonly ISmartContractAddressService _smartContractAddressService;

        public TokenContractChargeFeeStrategy(ISmartContractAddressService smartContractAddressService)
        {
            _smartContractAddressService = smartContractAddressService;
        }

        public Address ContractAddress =>
            _smartContractAddressService.GetAddressByContractName(TokenSmartContractAddressNameProvider.Name);

        public string MethodName => string.Empty;

        public bool IsFree(Transaction transaction)
        {
            // Stop charging fee from system txs and plugin txs.
            return new List<string>
            {
                // Pre-plugin tx
                nameof(TokenContractContainer.TokenContractStub.ChargeTransactionFees),
                nameof(TokenContractContainer.TokenContractStub.CheckThreshold),
                nameof(TokenContractContainer.TokenContractStub.CheckResourceToken),

                // Post-plugin tx
                nameof(TokenContractContainer.TokenContractStub.ChargeResourceToken),
            }.Contains(transaction.MethodName);
        }
    }
}