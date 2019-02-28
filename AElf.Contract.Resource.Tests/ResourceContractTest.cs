﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices.ComTypes;
using System.Threading.Tasks;
using AElf.Common;
using AElf.Contracts.Consensus.DPoS;
using AElf.Contracts.Genesis;
using AElf.Contracts.Resource.FeeReceiver;
using AElf.Contracts.TestBase;
using AElf.Contracts.Token;
using AElf.Cryptography;
using AElf.Cryptography.ECDSA;
using AElf.Kernel;
using AElf.Types.CSharp;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;
using Shouldly;
using Volo.Abp.Threading;

namespace AElf.Contracts.Resource.Tests
{
    public class ResourceContractTest: ResourceContractTestBase
    {
        private ContractTester Tester;
        private ECKeyPair FeeKeyPair;
        private ECKeyPair FoundationKeyPair;

        private Address BasicZeroContractAddress;
        private Address TokenContractAddress;
        private Address ResourceContractAddress;
        private Address FeeReceiverContractAddress;

        public ResourceContractTest()
        {
            Tester = new ContractTester();
            var contractArray = Tester.GetDefaultContractTypes();
            contractArray.Add(typeof(FeeReceiverContract));
            AsyncHelper.RunSync(() => Tester.InitialChainAsync(contractArray.ToArray()));

            BasicZeroContractAddress = Tester.DeployedContractsAddresses[(int)ContractConsts.GenesisBasicContract];
            TokenContractAddress = Tester.DeployedContractsAddresses[(int)ContractConsts.TokenContract];
            ResourceContractAddress = Tester.DeployedContractsAddresses[(int)ContractConsts.ResourceContract];
            FeeReceiverContractAddress = Tester.DeployedContractsAddresses[contractArray.Count - 1];

            FeeKeyPair = CryptoHelpers.GenerateKeyPair();
            FoundationKeyPair = CryptoHelpers.GenerateKeyPair();
        }

        [Fact]
        public async Task Deploy_Contracts()
        {
            var tokenTx = Tester.GenerateTransaction(BasicZeroContractAddress, "DeploySmartContract", 2,
                File.ReadAllBytes(typeof(TokenContract).Assembly.Location));
            var resourceTx = Tester.GenerateTransaction(BasicZeroContractAddress, "DeploySmartContract", 2,
                File.ReadAllBytes(typeof(ResourceContract).Assembly.Location));

            await Tester.MineABlockAsync(new List<Transaction> {tokenTx, resourceTx});
            var chain = await Tester.GetChainAsync();
            chain.LongestChainHeight.ShouldBeGreaterThanOrEqualTo(1UL);
        }

        [Fact]
        public async Task Initize_Resource()
        {
            //init token contract
            var initResult = await Tester.ExecuteContractWithMiningAsync(TokenContractAddress, "Initialize",
                "ELF", "elf token", 1000_000UL, 2U);
            initResult.Status.ShouldBe(TransactionResultStatus.Mined);

            //init fee receiver contract
            var foundationAddress = Tester.GetAddress(FoundationKeyPair);
            var feeReceiverResult = await Tester.ExecuteContractWithMiningAsync(FeeReceiverContractAddress, "Initialize",
                TokenContractAddress, foundationAddress);
            feeReceiverResult.Status.ShouldBe(TransactionResultStatus.Mined);

            //init resource contract
            var feeAddress = Tester.GetAddress(FeeKeyPair);
            var resourceResult = await Tester.ExecuteContractWithMiningAsync(ResourceContractAddress, "Initialize",
                TokenContractAddress, feeAddress, feeAddress);
            resourceResult.Status.ShouldBe(TransactionResultStatus.Mined);
        }

        [Fact]
        public async Task Verify_FeeReceiverContract_Information()
        {
            await Initize_Resource();

            var addressResult = await Tester.CallContractMethodAsync(FeeReceiverContractAddress, "GetElfTokenAddress");
            addressResult.DeserializeToString().ShouldBe(TokenContractAddress.GetFormatted());

            var foundationAddress = Tester.GetAddress(FoundationKeyPair).GetFormatted();
            var address1Result = await Tester.CallContractMethodAsync(FeeReceiverContractAddress, "GetFoundationAddress");
            address1Result.DeserializeToString().ShouldBe(foundationAddress);

            var balanceResult = await Tester.CallContractMethodAsync(FeeReceiverContractAddress, "GetOwedToFoundation");
            balanceResult.DeserializeToUInt64().ShouldBe(0u);
        }

        [Fact]
        public async Task Verify_Resource_AddressInfo()
        {
            await Initize_Resource();

            //verify result
            var tokenAddress = await Tester.CallContractMethodAsync(ResourceContractAddress, "GetElfTokenAddress");
            tokenAddress.DeserializeToString().ShouldBe(TokenContractAddress.GetFormatted());

            var address = Tester.GetAddress(FeeKeyPair);
            var feeAddressString = address.GetFormatted();
            var feeAddress = await Tester.CallContractMethodAsync(ResourceContractAddress, "GetFeeAddress");
            feeAddress.DeserializeToString().ShouldBe(feeAddressString);

            var controllerAddress = await Tester.CallContractMethodAsync(ResourceContractAddress, "GetResourceControllerAddress");
            controllerAddress.DeserializeToString().ShouldBe(feeAddressString);
        }

        [Fact]
        public async Task Query_Rsource_ConverterInfo()
        {
            await Initize_Resource();

            var cpuConverter = await Tester.CallContractMethodAsync(ResourceContractAddress, "GetConverter", "Cpu");
            var cpuString = cpuConverter.DeserializeToString();
            var cpuObj = JsonConvert.DeserializeObject<JObject>(cpuString);
            cpuObj["ResBalance"].ToObject<ulong>().ShouldBe(1000_000UL);
            cpuObj["ResWeight"].ToObject<ulong>().ShouldBe(500_000UL);
            cpuObj["ResourceType"].ToObject<string>().ShouldBe("Cpu");
        }

        [Fact]
        public async Task Query_Exchange_Balance()
        {
            await Initize_Resource();

            var exchangeResult = await Tester.CallContractMethodAsync(ResourceContractAddress, "GetExchangeBalance", "Cpu");
            exchangeResult.DeserializeToUInt64().ShouldBe(1000_000UL);
        }

        [Fact]
        public async Task Query_Elf_Balance()
        {
            await Initize_Resource();

            var elfResult = await Tester.CallContractMethodAsync(ResourceContractAddress, "GetElfBalance", "Cpu");
            elfResult.DeserializeToUInt64().ShouldBe(1000_000UL);
        }

        [Fact]
        public async Task IssueResource_With_Conntroller_Account()
        {
            await Initize_Resource();

            Tester.SetCallOwner(FeeKeyPair);
            var issueResult = await Tester.ExecuteContractWithMiningAsync(ResourceContractAddress,
                "IssueResource",
                "Cpu", 100_000UL);
            issueResult.Status.ShouldBe(TransactionResultStatus.Mined);

            //check result
            var cpuConverter = await Tester.CallContractMethodAsync(ResourceContractAddress, "GetConverter", "Cpu");
            var cpuString = cpuConverter.DeserializeToString();
            var cpuObj = JsonConvert.DeserializeObject<JObject>(cpuString);
            cpuObj["ResBalance"].ToObject<ulong>().ShouldBe(1000_000UL + 100_000UL);
        }

        [Fact]
        public async Task IssueResource_WithNot_Conntroller_Account()
        {
            await Initize_Resource();

            var otherKeyPair = CryptoHelpers.GenerateKeyPair();
            Tester.SetCallOwner(otherKeyPair);
            var issueResult = await Tester.ExecuteContractWithMiningAsync(ResourceContractAddress,
                "IssueResource",
                "CPU", 100_000UL);
            var returnMessage = issueResult.RetVal.ToStringUtf8();
            returnMessage.Contains("Only resource controller is allowed to perform this action.").ShouldBe(true);
            issueResult.Status.ShouldBe(TransactionResultStatus.Failed);
        }

        [Theory(Skip = "https://github.com/AElfProject/AElf/issues/952")]
        [InlineData(1000UL)]
        public async Task Buy_Resource_WithEnough_Token(ulong paidElf)
        {
            await Initize_Resource();
            var ownerAddress = Tester.GetAddress(Tester.CallOwnerKeyPair);

            //Approve first
            await ApproveBalance(paidElf);

            //Buy resouorce
            var buyResult = await Tester.ExecuteContractWithMiningAsync(ResourceContractAddress,
                "BuyResource",
                "Cpu", paidElf);
            var returnMessage = buyResult.RetVal.ToStringUtf8();
            returnMessage.ShouldBe(string.Empty);
            buyResult.Status.ShouldBe(TransactionResultStatus.Mined);

            //Check result
            var tokenBalance =
                await Tester.CallContractMethodAsync(TokenContractAddress, "BalanceOf", ownerAddress);
            tokenBalance.DeserializeToUInt64().ShouldBe(1000_000UL - paidElf);

            var cpuBalance = await Tester.CallContractMethodAsync(ResourceContractAddress, "GetUserBalance",
                ownerAddress, "Cpu");
            cpuBalance.DeserializeToUInt64().ShouldBeGreaterThan(0UL);
        }

        [Fact]
        public async Task Buy_Resource_WithoutEnough_Token()
        {
            await Initize_Resource();

            var noTokenKeyPair = CryptoHelpers.GenerateKeyPair();
            Tester.SetCallOwner(noTokenKeyPair);
            var buyResult = await Tester.ExecuteContractWithMiningAsync(ResourceContractAddress,
                "BuyResource",
                "Cpu", 10_000UL);
            var returnMessage = buyResult.RetVal.ToStringUtf8();
            returnMessage.Contains("").ShouldBe(true);
            buyResult.Status.ShouldBe(TransactionResultStatus.Failed);
        }

        [Fact(Skip = "https://github.com/AElfProject/AElf/issues/952")]
        public async Task Sell_WithEnough_Resource()
        {
            await Buy_Resource_WithEnough_Token(1000UL);

            var sellResult = await Tester.ExecuteContractWithMiningAsync(ResourceContractAddress,
                "SellResource",
                "Cpu", 100UL);
            sellResult.Status.ShouldBe(TransactionResultStatus.Mined);
        }

        [Fact(Skip = "https://github.com/AElfProject/AElf/issues/952")]
        public async Task Sell_WithoutEnough_Resource()
        {
            await Buy_Resource_WithEnough_Token(100UL);

            var sellResult = await Tester.ExecuteContractWithMiningAsync(ResourceContractAddress,
                "SellResource",
                "Cpu", 1000UL);
            var returnMessage = sellResult.RetVal.ToStringUtf8();
            returnMessage.Contains("Insufficient CPU balance.").ShouldBe(true);
            sellResult.Status.ShouldBe(TransactionResultStatus.Failed);
        }

        [Fact(Skip = "https://github.com/AElfProject/AElf/issues/952")]
        public async Task Lock_Available_Resource()
        {
            await Buy_Resource_WithEnough_Token(1000UL);

            var lockResult = await Tester.ExecuteContractWithMiningAsync(ResourceContractAddress, "LockResource",
                100UL, "Cpu");
            lockResult.Status.ShouldBe(TransactionResultStatus.Mined);
        }

        [Fact]
        public async Task Lock_OverOwn_Resource()
        {
            await Initize_Resource();

            var lockResult = await Tester.ExecuteContractWithMiningAsync(ResourceContractAddress, "LockResource",
                1000UL, "Cpu");
            var returnMessage = lockResult.RetVal.ToStringUtf8();
            returnMessage.Contains("System.OverflowException: Arithmetic operation resulted in an overflow.").ShouldBe(true);
            lockResult.Status.ShouldBe(TransactionResultStatus.Failed);
        }

        private async Task ApproveBalance(ulong amount)
        {
            var callOwner = Tester.GetAddress(Tester.CallOwnerKeyPair);
            var feeAddress = Tester.GetAddress(FeeKeyPair);

            var feeRate = new decimal(5, 0, 0, false, 3);
            var fees = (ulong) (amount * feeRate);
            var elfForRes = amount - fees;

            var resourceResult = await Tester.ExecuteContractWithMiningAsync(TokenContractAddress, "Approve",
                ResourceContractAddress, elfForRes);
            resourceResult.Status.ShouldBe(TransactionResultStatus.Mined);
            var allowanceResult1 = await Tester.CallContractMethodAsync(TokenContractAddress, "Allowance",
                callOwner, ResourceContractAddress);
            Console.WriteLine($"Allowance Query: {ResourceContractAddress} = {allowanceResult1.DeserializeToUInt64()}");

            var feeResult = await Tester.ExecuteContractWithMiningAsync(TokenContractAddress, "Approve",
                feeAddress, fees);
            feeResult.Status.ShouldBe(TransactionResultStatus.Mined);
            var allowanceResult2 = await Tester.CallContractMethodAsync(TokenContractAddress, "Allowance",
                callOwner, feeAddress);
            Console.WriteLine($"Allowance Query: {feeAddress} = {allowanceResult2.DeserializeToUInt64()}");
        }
    }
}