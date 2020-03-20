using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf.Contracts.MultiToken;
using AElf.Kernel.Miner.Application;
using AElf.Kernel.SmartContract.Application;
using AElf.Kernel.Token;
using AElf.Kernel.Txn.Application;
using AElf.Types;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AElf.Kernel.SmartContract.ExecutionPluginForResourceFee
{
    public class DonateResourceTransactionGenerator : ISystemTransactionGenerator
    {
        private readonly ISmartContractAddressService _smartContractAddressService;
        private readonly ITotalResourceTokensMapsProvider _totalResourceTokensMapsProvider;
        private readonly TransactionPackingOptions _transactionPackingOptions;

        public ILogger<DonateResourceTransactionGenerator> Logger { get; set; }


        public DonateResourceTransactionGenerator(ISmartContractAddressService smartContractAddressService,
            IOptionsMonitor<TransactionPackingOptions> transactionPackingOptions,
            ITotalResourceTokensMapsProvider totalResourceTokensMapsProvider)
        {
            _smartContractAddressService = smartContractAddressService;
            _totalResourceTokensMapsProvider = totalResourceTokensMapsProvider;
            _transactionPackingOptions = transactionPackingOptions.CurrentValue;
        }

        public async Task<List<Transaction>> GenerateTransactionsAsync(Address @from, long preBlockHeight,
            Hash preBlockHash)
        {
            var generatedTransactions = new List<Transaction>();
            if (!_transactionPackingOptions.IsTransactionPackable)
                return generatedTransactions;

            if (preBlockHeight < Constants.GenesisBlockHeight)
                return generatedTransactions;


            var tokenContractAddress = _smartContractAddressService.GetAddressByContractName(
                TokenSmartContractAddressNameProvider.Name);

            if (tokenContractAddress == null)
            {
                return generatedTransactions;
            }

            var totalResourceTokensMaps = await _totalResourceTokensMapsProvider.GetTotalResourceTokensMapsAsync(
                new ChainContext
                {
                    BlockHash = preBlockHash,
                    BlockHeight = preBlockHeight
                });
            if (totalResourceTokensMaps == null)
            {
                // If previous block doesn't contain logEvent named ResourceTokenCharged, won't generate this tx.
                return new List<Transaction>();
            }

            generatedTransactions.AddRange(new List<Transaction>
            {
                new Transaction
                {
                    From = from,
                    MethodName = nameof(TokenContractImplContainer.TokenContractImplStub.DonateResourceToken),
                    To = tokenContractAddress,
                    RefBlockNumber = preBlockHeight,
                    RefBlockPrefix = ByteString.CopyFrom(preBlockHash.Value.Take(4).ToArray()),
                    Params = totalResourceTokensMaps.ToByteString()
                }
            });

            Logger.LogInformation("Donate resource transaction generated.");
            return generatedTransactions;
        }
    }
}