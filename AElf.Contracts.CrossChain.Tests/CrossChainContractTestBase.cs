using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf.Common;
using AElf.Contracts.Authorization;
using AElf.Contracts.Consensus.DPoS;
using AElf.Contracts.CrossChain;
using AElf.Contracts.TestBase;
using AElf.Contracts.Token;
using AElf.CrossChain;
using AElf.Cryptography.ECDSA;
using AElf.Kernel;
using AElf.TestBase;
using Google.Protobuf;
using Shouldly;
using Volo.Abp.Threading;

namespace AElf.Contract.CrossChain.Tests
{
    public class CrossChainContractTestBase : ContractTestBase<CrossChainContractTestAElfModule>
    {
        protected Address CrossChainContractAddress;
        protected Address TokenContractAddress;
        protected Address ConsensusContractAddress;
        protected Address AuthorizationContractAddress;
        public CrossChainContractTestBase()
        {
            Tester = new ContractTester<CrossChainContractTestAElfModule>(CrossChainContractTestHelper.EcKeyPair);
            AsyncHelper.RunSync(() => Tester.InitialChainAsync(Tester.GetDefaultContractTypes().ToArray()));
            CrossChainContractAddress = Tester.GetContractAddress(Hash.FromString(typeof(CrossChainContract).FullName));
            TokenContractAddress = Tester.GetContractAddress(Hash.FromString(typeof(TokenContract).FullName));
            ConsensusContractAddress = Tester.GetContractAddress(Hash.FromString(typeof(ConsensusContract).FullName));
            AuthorizationContractAddress = Tester.GetContractAddress(Hash.FromString(typeof(AuthorizationContract).FullName));
        }

        protected async Task ApproveBalance(ulong amount)
        {
            var callOwner = Address.FromPublicKey(CrossChainContractTestHelper.GetPubicKey());

            var approveResult = await Tester.ExecuteContractWithMiningAsync(TokenContractAddress, "Approve",
                CrossChainContractAddress, amount);
            approveResult.Status.ShouldBe(TransactionResultStatus.Mined);
            await Tester.CallContractMethodAsync(TokenContractAddress, "Allowance",
                callOwner, CrossChainContractAddress);
        }

        protected async Task Initialize(ulong tokenAmount, int parentChainId = 0)
        {
            var tx1 = await Tester.GenerateTransactionAsync(TokenContractAddress, "Initialize",
                "ELF", "elf token", tokenAmount, 2U);
            var tx2 = await Tester.GenerateTransactionAsync(CrossChainContractAddress, "Initialize",
                ConsensusContractAddress, TokenContractAddress, AuthorizationContractAddress,
                parentChainId == 0 ? ChainHelpers.GetRandomChainId() : parentChainId);
            await Tester.MineAsync(new List<Transaction> {tx1, tx2});
        }

        protected async Task<int> InitAndCreateSideChain(int parentChainId = 0, ulong lockedTokenAmount = 10)
        {
            await Initialize(1000, parentChainId);
            
            await ApproveBalance(lockedTokenAmount);
            var sideChainInfo = new SideChainInfo
            {
                SideChainStatus = SideChainStatus.Apply,
                ContractCode = ByteString.Empty,
                IndexingPrice = 1,
                Proposer = CrossChainContractTestHelper.GetAddress(),
                LockedTokenAmount = lockedTokenAmount
            };
            
            var tx1 = await Tester.GenerateTransactionAsync(CrossChainContractAddress, "RequestChainCreation",
                sideChainInfo);
            await Tester.MineAsync(new List<Transaction> {tx1});
            var chainId = ChainHelpers.GetChainId(1);
            var tx2 = await  Tester.GenerateTransactionAsync(CrossChainContractAddress, "CreateSideChain", chainId);
            await Tester.MineAsync(new List<Transaction> {tx2});
            return chainId;
        }

        protected async Task<Block> MineAsync(List<Transaction> txs)
        {
            return await Tester.MineAsync(txs);
        }
        
        protected  async Task<TransactionResult> ExecuteContractWithMiningAsync(Address contractAddress, string methodName, params object[] objects)
        {
            return await Tester.ExecuteContractWithMiningAsync(contractAddress, methodName, objects);
        }

        protected async Task<Transaction> GenerateTransactionAsync(Address contractAddress, string methodName, ECKeyPair ecKeyPair = null, params object[] objects)
        {
            return ecKeyPair == null
                ? await Tester.GenerateTransactionAsync(contractAddress, methodName, objects)
                : await Tester.GenerateTransactionAsync(contractAddress, methodName, ecKeyPair, objects);
        }

        protected async Task<TransactionResult> GetTransactionResult(Hash txId)
        {
            return await Tester.GetTransactionResult(txId);
        }

        protected async Task<ByteString> CallContractMethodAsync(Address contractAddress, string methodName,
            params object[] objects)
        {
            return await Tester.CallContractMethodAsync(contractAddress, methodName, objects);
        }
        
        protected byte[] GetFriendlyBytes(int value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(bytes);
            return bytes.Skip(Array.FindIndex(bytes, Convert.ToBoolean)).ToArray();
        }
    }
}