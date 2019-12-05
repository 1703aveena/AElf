using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Acs7;
using AElf.Contracts.CrossChain;
using AElf.Kernel.Miner.Application;
using AElf.Kernel.SmartContract.Application;
using AElf.Types;
using Google.Protobuf;
using Microsoft.Extensions.Logging;

namespace AElf.CrossChain
{
    internal class CrossChainIndexingTransactionGenerator : ISystemTransactionGenerator
    {
        private readonly ICrossChainIndexingDataService _crossChainIndexingDataService;

        private readonly ISmartContractAddressService _smartContractAddressService;

        public ILogger<CrossChainIndexingTransactionGenerator> Logger { get; set; }

        public CrossChainIndexingTransactionGenerator(ICrossChainIndexingDataService crossChainIndexingDataService,
            ISmartContractAddressService smartContractAddressService)
        {
            _crossChainIndexingDataService = crossChainIndexingDataService;
            _smartContractAddressService = smartContractAddressService;
        }

        private async Task<List<Transaction>> GenerateCrossChainIndexingTransactionAsync(Address from, long refBlockNumber,
            Hash previousBlockHash)
        {
            var generatedTransactions = new List<Transaction>();

            var proposingInput = new ProposeCrossChainIndexingInput();
            // should return the same data already filled in block header.
            var crossChainBlockData =
                await _crossChainIndexingDataService.GetCrossChainBlockDataForNextMiningAsync(previousBlockHash, refBlockNumber);
            

            if (!crossChainBlockData.IsNullOrEmpty())
            {            
                proposingInput.ProposedCrossChainData = crossChainBlockData;
            }
            
            var previousBlockPrefix = previousBlockHash.Value.Take(4).ToArray();
            generatedTransactions.Add(GenerateNotSignedTransaction(from,
                nameof(CrossChainContractContainer.CrossChainContractStub.ProposeCrossChainIndexing), refBlockNumber,
                previousBlockPrefix, proposingInput));
            
            Logger.LogTrace($"{generatedTransactions.Count} Cross chain transaction generated.");
            return generatedTransactions;
        }

        public async Task<List<Transaction>> GenerateTransactionsAsync(Address @from, long preBlockHeight, Hash preBlockHash)
        {
            var generatedTransactions =
                await GenerateCrossChainIndexingTransactionAsync(from, preBlockHeight, preBlockHash);
            return generatedTransactions;
        }

        /// <summary>
        /// Create a txn with provided data.
        /// </summary>
        /// <param name="from"></param>
        /// <param name="methodName"></param>
        /// <param name="refBlockNumber"></param>
        /// <param name=""></param>
        /// <param name="refBlockPrefix"></param> 
        /// <param name="input"></param>
        /// <returns></returns>
        private Transaction GenerateNotSignedTransaction(Address from, string methodName, long refBlockNumber,
            byte[] refBlockPrefix, IMessage input)
        {
            return new Transaction
            {
                From = from,
                To = _smartContractAddressService.GetAddressByContractName(
                    CrossChainSmartContractAddressNameProvider.Name),
                RefBlockNumber = refBlockNumber,
                RefBlockPrefix = ByteString.CopyFrom(refBlockPrefix),
                MethodName = methodName,
                Params = input.ToByteString(),
            };
        }
    }
}