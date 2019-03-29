using AElf.Contracts.Dividend;
using AElf.Contracts.MultiToken;
using AElf.Contracts.MultiToken.Messages;
using AElf.Contracts.Resource.FeeReceiver;
using AElf.Contracts.TestBase;
using AElf.Kernel;
using AElf.Kernel.Consensus;
using AElf.Kernel.SmartContract;
using AElf.Kernel.Token;
using AElf.OS.Node.Application;
using System.Threading.Tasks;
using AElf.Common;
using AElf.Kernel.SmartContract.Application;
using Google.Protobuf;
using Microsoft.Extensions.DependencyInjection;
using Nito.AsyncEx;
using Volo.Abp;

namespace AElf.Contracts.TokenConverter
{
    public static class TokenConverterContractTesterExtension
    {
        public static async Task InitialChainAndTokenAsync(this ContractTester<TokenConverterTestAElfModule> starter)
        {
            var tokenContractCallList = new SystemTransactionMethodCallList();
            tokenContractCallList.Add(nameof(TokenContract.CreateNativeToken), new CreateNativeTokenInput
            {
                Symbol = "ELF",
                Decimals = 2,
                IsBurnable = true,
                TokenName = "elf token",
                TotalSupply = 1000_0000L,
                Issuer = starter.GetCallOwnerAddress(),
                LockWhiteSystemContractNameList = {ConsensusSmartContractAddressNameProvider.Name}
            });
            
            // For testing.
            tokenContractCallList.Add(nameof(TokenContract.Issue), new IssueInput
            {
                Symbol = "ELF",
                Amount = 1000_000L,
                To = starter.GetCallOwnerAddress(),
                Memo = "Set for token converter.",
            });
            
            await starter.InitialChainAsync(
                list =>
                {
                    // Dividends contract must be deployed before token contract.
                    list.AddGenesisSmartContract<DividendContract>(DividendsSmartContractAddressNameProvider.Name);
                    list.AddGenesisSmartContract<TokenContract>(TokenSmartContractAddressNameProvider.Name, tokenContractCallList);
                    list.AddGenesisSmartContract<FeeReceiverContract>(ResourceFeeReceiverSmartContractAddressNameProvider.Name);
                    list.AddGenesisSmartContract<TokenConverterContract>(TokenConverterSmartContractAddressNameProvider.Name);
                });
        }

        public static async Task<ByteString> CallTokenConverterMethodAsync(
            this ContractTester<TokenConverterTestAElfModule> starter, string methodName, IMessage input)
        {
            return await starter.CallContractMethodAsync(starter.GetContractAddress(TokenConverterSmartContractAddressNameProvider.Name),
                methodName, input);
        }

        public static async Task<TransactionResult> ExecuteTokenConverterMethodAsync(
            this ContractTester<TokenConverterTestAElfModule> starter, string methodName, IMessage input)
        {
            return await starter.ExecuteContractWithMiningAsync(starter.GetContractAddress(TokenConverterSmartContractAddressNameProvider.Name),
                methodName, input);
        }
        
        public static async Task<long> GetBalanceAsync(this ContractTester<TokenConverterTestAElfModule> starter,
            Address targetAddress, string symbol)
        {
            var bytes = await starter.CallContractMethodAsync(starter.GetContractAddress(TokenSmartContractAddressNameProvider.Name),
                nameof(TokenContract.GetBalance), new GetBalanceInput
                {
                    Owner = targetAddress,
                    Symbol = symbol
                });
            var balanceOutput = GetBalanceOutput.Parser.ParseFrom(bytes);
            return balanceOutput.Balance;
        }
    }
}