﻿using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using AElf.Common;
using AElf.Contracts.MultiToken;
using AElf.Contracts.MultiToken.Messages;
using AElf.Contracts.TestBase;
using AElf.Cryptography;
using AElf.Cryptography.ECDSA;
using AElf.Kernel;
using AElf.Kernel.KernelAccount;
using AElf.Kernel.Token;
using AElf.Types.CSharp;
using Google.Protobuf;
using Xunit;
using Shouldly;
using Volo.Abp.Threading;

namespace AElf.Contracts.Token
{
    public sealed class TokenContractTest : ContractTestBase<TokenContractTestAElfModule>
    {
        private readonly ECKeyPair _spenderKeyPair;
        private Address BasicZeroContractAddress { get; set; }
        private Address TokenContractAddress { get; set; }

        private const string Symbol = "ELFTEST";

        private const int DefaultCategory = 3;

        public TokenContractTest()
        {
            AsyncHelper.RunSync(() => Tester.InitialChainAsync(Tester.GetDefaultContractTypes(Tester.GetCallOwnerAddress())));
            BasicZeroContractAddress = Tester.GetZeroContractAddress();
            TokenContractAddress = Tester.GetContractAddress(TokenSmartContractAddressNameProvider.Name);
            _spenderKeyPair = CryptoHelpers.GenerateKeyPair();
        }

        [Fact]
        public async Task Deploy_TokenContract()
        {
            var tx = await Tester.GenerateTransactionAsync(BasicZeroContractAddress,
                nameof(ISmartContractZero.DeploySmartContract), DefaultCategory,
                File.ReadAllBytes(typeof(TokenContract).Assembly.Location));

            await Tester.MineAsync(new List<Transaction> {tx});
            var chain = await Tester.GetChainAsync();
            chain.LongestChainHeight.ShouldBeGreaterThanOrEqualTo(1);
        }

        [Fact]
        public async Task Deploy_TokenContract_Twice()
        {
            var bytes = await Tester.CallContractMethodAsync(BasicZeroContractAddress,
                nameof(ISmartContractZero.DeploySystemSmartContract), new SystemContractDeploymentInput
                {
                    Category = DefaultCategory,
                    Code = ByteString.CopyFrom(File.ReadAllBytes(typeof(TokenContract).Assembly.Location))
                }
            );
            // Failed to deploy.
            Assert.Empty(bytes);
        }

        [Fact]
        public async Task Initialize_TokenContract()
        {
            var tx = await Tester.GenerateTransactionAsync(TokenContractAddress, nameof(TokenContract.Create),
                new CreateInput
                {
                    Symbol = Symbol,
                    Decimals = 2,
                    IsBurnable = true,
                    Issuer = Tester.GetCallOwnerAddress(),
                    TokenName = "elf token",
                    TotalSupply = 1000_000L
                });
            var issueTx = await Tester.GenerateTransactionAsync(TokenContractAddress, nameof(TokenContract.Issue),
                new IssueInput
                {
                    Symbol = Symbol,
                    Amount = 1000_000L,
                    To = Tester.GetCallOwnerAddress(),
                    Memo = "Issue token to starter himself."
                });
            await Tester.MineAsync(new List<Transaction> {tx, issueTx});
            var bytes = await Tester.CallContractMethodAsync(TokenContractAddress, nameof(TokenContract.GetBalance),
                new GetBalanceInput
                {
                    Symbol = Symbol,
                    Owner = Tester.GetCallOwnerAddress()
                });
            var result = GetBalanceOutput.Parser.ParseFrom(bytes);
            result.Balance.ShouldBe(1000_000L);
        }

        [Fact]
        public async Task Initialize_View_TokenContract()
        {
            var tx = await Tester.GenerateTransactionAsync(TokenContractAddress, nameof(TokenContract.Create),
                new CreateInput
                {
                    Symbol = Symbol,
                    Decimals = 2,
                    IsBurnable = true,
                    Issuer = Address.FromPublicKey(_spenderKeyPair.PublicKey),
                    TokenName = "elf token",
                    TotalSupply = 1000_000L
                });
            await Tester.MineAsync(new List<Transaction> {tx});

            var bytes = await Tester.CallContractMethodAsync(TokenContractAddress, nameof(TokenContract.GetTokenInfo),
                new GetTokenInfoInput
                {
                    Symbol = Symbol,
                });
            var tokenInfo = bytes.DeserializeToPbMessage<TokenInfo>();
            tokenInfo.Symbol.ShouldBe(Symbol);
            tokenInfo.Decimals.ShouldBe(2);
            tokenInfo.IsBurnable.ShouldBe(true);
            tokenInfo.Issuer.ShouldBe(Address.FromPublicKey(_spenderKeyPair.PublicKey));
            tokenInfo.TokenName.ShouldBe("elf token");
            tokenInfo.TotalSupply.ShouldBe(1000_000L);
        }

        [Fact]
        public async Task Initialize_TokenContract_Failed()
        {
            await Initialize_TokenContract();

            var otherKeyPair = CryptoHelpers.GenerateKeyPair();
            var other = Tester.CreateNewContractTester(otherKeyPair);
            var result = await other.ExecuteContractWithMiningAsync(TokenContractAddress,
                nameof(TokenContract.Create), new CreateInput
                {
                    Symbol = Symbol,
                    Decimals = 2,
                    IsBurnable = false,
                    Issuer = Address.FromPublicKey(_spenderKeyPair.PublicKey),
                    TokenName = "elf token",
                    TotalSupply = 1000_000L
                });
            result.Status.ShouldBe(TransactionResultStatus.Failed);
            result.Error.Contains("Token already exists.").ShouldBeTrue();
        }

        [Fact]
        public async Task Transfer_TokenContract()
        {
            await Initialize_TokenContract();

            var toAddress = CryptoHelpers.GenerateKeyPair();
            await Tester.ExecuteContractWithMiningAsync(TokenContractAddress, nameof(TokenContract.Transfer),
                new TransferInput
                {
                    Amount = 1000L,
                    Memo = "transfer test",
                    Symbol = Symbol,
                    To = Tester.GetAddress(toAddress)
                });

            var bytes1 =
                await Tester.CallContractMethodAsync(TokenContractAddress, nameof(TokenContract.GetBalance),
                    new GetBalanceInput
                    {
                        Symbol = Symbol,
                        Owner = Tester.GetCallOwnerAddress()
                    });
            var bytes2 =
                await Tester.CallContractMethodAsync(TokenContractAddress, nameof(TokenContract.GetBalance),
                    new GetBalanceInput
                    {
                        Symbol = Symbol,
                        Owner = Tester.GetAddress(toAddress)
                    });
            bytes1.DeserializeToPbMessage<GetBalanceOutput>().Balance.ShouldBe(1000_000L - 1000L);
            bytes2.DeserializeToPbMessage<GetBalanceOutput>().Balance.ShouldBe(1000L);
        }

        [Fact]
        public async Task Transfer_Without_Enough_Token()
        {
            await Initialize_TokenContract();

            var toAddress = CryptoHelpers.GenerateKeyPair();
            var fromAddress = CryptoHelpers.GenerateKeyPair();
            var from = Tester.CreateNewContractTester(fromAddress);

            var result = from.ExecuteContractWithMiningAsync(TokenContractAddress, nameof(TokenContract.Transfer),
                new TransferInput
                {
                    Amount = 1000L,
                    Memo = "transfer test",
                    Symbol = Symbol,
                    To = from.GetAddress(toAddress)
                });
            result.Result.Status.ShouldBe(TransactionResultStatus.Failed);
            var bytes = await from.CallContractMethodAsync(TokenContractAddress, nameof(TokenContract.GetBalance),
                new GetBalanceInput
                {
                    Owner = from.GetAddress(fromAddress),
                    Symbol = Symbol
                });
            var balance = bytes.DeserializeToPbMessage<GetBalanceOutput>().Balance;
            balance.ShouldBe(0L);
            result.Result.Error.Contains($"Insufficient balance").ShouldBeTrue();
        }

        [Fact]
        public async Task Approve_TokenContract()
        {
            await Initialize_TokenContract();

            var owner = Tester.GetCallOwnerAddress();
            var spender = Tester.GetAddress(_spenderKeyPair);

            var result1 = await Tester.ExecuteContractWithMiningAsync(TokenContractAddress,
                nameof(TokenContract.Approve), new ApproveInput
                {
                    Symbol = Symbol,
                    Amount = 2000L,
                    Spender = spender
                });
            result1.Status.ShouldBe(TransactionResultStatus.Mined);
            var bytes1 = await Tester.CallContractMethodAsync(TokenContractAddress, nameof(TokenContract.GetAllowance),
                new GetAllowanceInput
                {
                    Owner = owner,
                    Spender = spender,
                    Symbol = Symbol
                });
            bytes1.DeserializeToPbMessage<GetAllowanceOutput>().Allowance.ShouldBe(2000L);
        }

        [Fact]
        public async Task UnApprove_TokenContract()
        {
            await Approve_TokenContract();
            var owner = Tester.GetCallOwnerAddress();
            var spender = Tester.GetAddress(_spenderKeyPair);

            var result2 =
                await Tester.ExecuteContractWithMiningAsync(TokenContractAddress, nameof(TokenContract.UnApprove),
                    new UnApproveInput
                    {
                        Amount = 1000L,
                        Symbol = Symbol,
                        Spender = spender
                    });
            result2.Status.ShouldBe(TransactionResultStatus.Mined);
            var bytes2 = await Tester.CallContractMethodAsync(TokenContractAddress, nameof(TokenContract.GetAllowance),
                new GetAllowanceInput
                {
                    Owner = owner,
                    Spender = spender,
                    Symbol = Symbol
                });
            bytes2.DeserializeToPbMessage<GetAllowanceOutput>().Allowance.ShouldBe(2000L - 1000L);
        }

        [Fact]
        public async Task UnApprove_Without_Enough_Allowance()
        {
            await Initialize_TokenContract();

            var owner = Tester.GetCallOwnerAddress();
            var spender = Tester.GetAddress(_spenderKeyPair);

            var bytes = await Tester.CallContractMethodAsync(TokenContractAddress, nameof(TokenContract.GetAllowance),
                new GetAllowanceInput
                {
                    Owner = owner,
                    Spender = spender,
                    Symbol = Symbol
                });
            bytes.DeserializeToPbMessage<GetAllowanceOutput>().Allowance.ShouldBe(0L);
            var result = await Tester.ExecuteContractWithMiningAsync(TokenContractAddress,
                nameof(TokenContract.UnApprove), new UnApproveInput
                {
                    Amount = 1000L,
                    Spender = spender,
                    Symbol = Symbol
                });
            result.Status.ShouldBe(TransactionResultStatus.Mined);
        }

        [Fact]
        public async Task TransferFrom_TokenContract()
        {
            await Approve_TokenContract();

            var owner = Tester.GetCallOwnerAddress();
            var spenderAddress = Tester.GetAddress(_spenderKeyPair);

            var spender = Tester.CreateNewContractTester(_spenderKeyPair);
            var result2 =
                await spender.ExecuteContractWithMiningAsync(TokenContractAddress, nameof(TokenContract.TransferFrom),
                    new TransferFromInput
                    {
                        Amount = 1000L,
                        From = owner,
                        Memo = "test",
                        Symbol = Symbol,
                        To = spenderAddress
                    });
            result2.Status.ShouldBe(TransactionResultStatus.Mined);
            var bytes2 = await spender.CallContractMethodAsync(TokenContractAddress, nameof(TokenContract.GetAllowance),
                new GetAllowanceInput
                {
                    Owner = owner,
                    Spender = spenderAddress,
                    Symbol = Symbol
                });
            bytes2.DeserializeToPbMessage<GetAllowanceOutput>().Allowance.ShouldBe(2000L - 1000L);

            var bytes3 = await spender.CallContractMethodAsync(TokenContractAddress, nameof(TokenContract.GetBalance),
                new GetBalanceInput
                {
                    Owner = spenderAddress,
                    Symbol = Symbol
                });
            bytes3.DeserializeToPbMessage<GetBalanceOutput>().Balance.ShouldBe(1000L);
        }

        [Fact]
        public async Task TransferFrom_With_ErrorAccount()
        {
            await Approve_TokenContract();

            var owner = Tester.GetCallOwnerAddress();
            var spender = Tester.GetAddress(_spenderKeyPair);

            var result2 =
                await Tester.ExecuteContractWithMiningAsync(TokenContractAddress, nameof(TokenContract.TransferFrom),
                    new TransferFromInput
                    {
                        Amount = 1000L,
                        From = owner,
                        Memo = "transfer from test",
                        Symbol = Symbol,
                        To = spender
                    });
            result2.Status.ShouldBe(TransactionResultStatus.Failed);
            result2.Error.Contains("Insufficient allowance.").ShouldBeTrue();

            var bytes2 = await Tester.CallContractMethodAsync(TokenContractAddress, nameof(TokenContract.GetAllowance),
                new GetAllowanceInput
                {
                    Owner = owner,
                    Spender = spender,
                    Symbol = Symbol
                });
            bytes2.DeserializeToPbMessage<GetAllowanceOutput>().Allowance.ShouldBe(2000L);

            var bytes3 =
                await Tester.CallContractMethodAsync(TokenContractAddress, nameof(TokenContract.GetBalance),
                    new GetBalanceInput
                    {
                        Owner = spender,
                        Symbol = Symbol
                    });
            bytes3.DeserializeToPbMessage<GetBalanceOutput>().Balance.ShouldBe(0L);
        }

        [Fact]
        public async Task TransferFrom_Without_Enough_Allowance()
        {
            await Initialize_TokenContract();
            var owner = Tester.GetCallOwnerAddress();
            var spender = Tester.GetAddress(_spenderKeyPair);

            var bytes = await Tester.CallContractMethodAsync(TokenContractAddress, nameof(TokenContract.GetAllowance),
                new GetAllowanceInput
                {
                    Owner = owner,
                    Spender = spender,
                    Symbol = Symbol
                });
            bytes.DeserializeToPbMessage<GetAllowanceOutput>().Allowance.ShouldBe(0L);

            //Tester.SetCallOwner(spenderKeyPair);
            var result =
                await Tester.ExecuteContractWithMiningAsync(TokenContractAddress, nameof(TokenContract.TransferFrom),
                    new TransferFromInput
                    {
                        Amount = 1000L,
                        From = owner,
                        Memo = "transfer from test",
                        Symbol = Symbol,
                        To = spender
                    });
            result.Status.ShouldBe(TransactionResultStatus.Failed);
            result.Error.Contains("Insufficient allowance.").ShouldBeTrue();
        }

        [Fact]
        public async Task Burn_TokenContract()
        {
            await Initialize_TokenContract();
            await Tester.ExecuteContractWithMiningAsync(TokenContractAddress, nameof(TokenContract.Burn), new BurnInput
            {
                Amount = 3000L,
                Symbol = Symbol
            });
            var bytes = await Tester.CallContractMethodAsync(TokenContractAddress, nameof(TokenContract.GetBalance),
                new GetBalanceInput
                {
                    Owner = Tester.GetCallOwnerAddress(),
                    Symbol = Symbol
                });
            bytes.DeserializeToPbMessage<GetBalanceOutput>().Balance.ShouldBe(1000_000L - 3000L);
        }

        [Fact]
        public async Task Burn_Without_Enough_Balance()
        {
            await Initialize_TokenContract();
            var burnerAddress = CryptoHelpers.GenerateKeyPair();
            var burner = Tester.CreateNewContractTester(burnerAddress);
            var result = await burner.ExecuteContractWithMiningAsync(TokenContractAddress, nameof(TokenContract.Burn),
                new BurnInput
                {
                    Symbol = Symbol,
                    Amount = 3000L
                });
            result.Status.ShouldBe(TransactionResultStatus.Failed);
            result.Error.Contains("Burner doesn't own enough balance.").ShouldBeTrue();
        }

        [Fact]
        public async Task Charge_Transaction_Fees()
        {
            await Initialize_TokenContract();

            var result =
                await Tester.ExecuteContractWithMiningAsync(TokenContractAddress,
                    nameof(TokenContract.ChargeTransactionFees), new ChargeTransactionFeesInput
                    {
                        Amount = 10L,
                        Symbol = "ELF"
                    });
            result.Status.ShouldBe(TransactionResultStatus.Mined);
            await Tester.ExecuteContractWithMiningAsync(TokenContractAddress, nameof(TokenContract.Transfer),
                new TransferInput
                {
                    Symbol = "ELF",
                    Amount = 1000L,
                    Memo = "transfer test",
                    To = Tester.GetAddress(_spenderKeyPair)
                });
            var bytes1 = await Tester.CallContractMethodAsync(TokenContractAddress, nameof(TokenContract.GetBalance),
                new GetBalanceInput
                {
                    Owner = Tester.GetCallOwnerAddress(),
                    Symbol = "ELF"
                });
            bytes1.DeserializeToPbMessage<GetBalanceOutput>().Balance.ShouldBe(800_000L - 1000L - 10L);
        }

        [Fact]
        public async Task Claim_Transaction_Fees_Without_FeePoolAddress()
        {
            await Initialize_TokenContract();
            var result = await Tester.ExecuteContractWithMiningAsync(TokenContractAddress,
                nameof(TokenContract.ClaimTransactionFees), new ClaimTransactionFeesInput
                {
                    Symbol = "ELF",
                    Height = 1L
                });
            result.Status.ShouldBe(TransactionResultStatus.Failed);
            result.Error.Contains("Fee pool address is not set.").ShouldBeTrue();
        }

        [Fact]
        public async Task Set_And_Get_Method_Fee()
        {
            await Initialize_TokenContract();

            {
                var resultGetBytes = await Tester.CallContractMethodAsync(TokenContractAddress,
                    nameof(TokenContract.GetMethodFee), new GetMethodFeeInput
                    {
                        Method = nameof(TokenContract.Transfer)
                    });
                var resultGet = GetMethodFeeOutput.Parser.ParseFrom(resultGetBytes);
                resultGet.Fee.ShouldBe(0L);
            }

            var resultSet = await Tester.ExecuteContractWithMiningAsync(TokenContractAddress,
                nameof(TokenContract.SetMethodFee), new SetMethodFeeInput
                {
                    Method = nameof(TokenContract.Transfer),
                    Fee = 10L
                });
            resultSet.Status.ShouldBe(TransactionResultStatus.Mined);

            {
                var resultGetBytes = await Tester.CallContractMethodAsync(TokenContractAddress,
                    nameof(TokenContract.GetMethodFee), new GetMethodFeeInput
                    {
                        Method = nameof(TokenContract.Transfer)
                    });
                var resultGet = GetMethodFeeOutput.Parser.ParseFrom(resultGetBytes);
                resultGet.Fee.ShouldBe(10L);
            }
        }
    }
}