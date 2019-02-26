﻿using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using AElf.Common;
using AElf.Contracts.Consensus.DPoS;
using AElf.Contracts.Genesis;
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

        private Address BasicZeroContractAddress;
        private Address TokenContractAddress;
        private Address ResourceContractAddress;

        public ResourceContractTest()
        {
            Tester = new ContractTester();
            AsyncHelper.RunSync(() => Tester.InitialChainAsync(Tester.GetDefaultContractTypes().ToArray()));
            BasicZeroContractAddress = Tester.DeployedContractsAddresses[(int)ContractConsts.GenesisBasicContract];
            TokenContractAddress = Tester.DeployedContractsAddresses[(int)ContractConsts.TokenContract];
            ResourceContractAddress = Tester.DeployedContractsAddresses[(int)ContractConsts.ResourceContract];
            FeeKeyPair = CryptoHelpers.GenerateKeyPair();
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
            var initResult = await Tester.ExecuteContractWithMiningAsync(TokenContractAddress, "Initialize",
                "ELF", "elf token", 1000_000UL, 2U);
            initResult.Status.ShouldBe(TransactionResultStatus.Mined);
            var feeAddress = Tester.GetAddress(FeeKeyPair);
            var resourceResult = await Tester.ExecuteContractWithMiningAsync(ResourceContractAddress, "Initialize",
                TokenContractAddress, feeAddress, feeAddress);
            resourceResult.Status.ShouldBe(TransactionResultStatus.Mined);
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

        [Fact]
        public async Task Buy_Resource_WithEnough_Token()
        {
            await Initize_Resource();

            var ownerAddress = Tester.GetAddress(Tester.CallOwnerKeyPair);
            var buyResult = await Tester.ExecuteContractWithMiningAsync(ResourceContractAddress,
                "BuyResource",
                "Cpu", 10_000UL);
            var returnMessage = buyResult.RetVal.ToStringUtf8();
            buyResult.Status.ShouldBe(TransactionResultStatus.Mined);

            //check result
            var tokenBalance2 =
                await Tester.CallContractMethodAsync(TokenContractAddress, "BalanceOf", ownerAddress);
            tokenBalance2.DeserializeToUInt64().ShouldBe(1000_000UL - 10_000UL);

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
            buyResult.Status.ShouldBe(TransactionResultStatus.Failed);
        }
    }
}