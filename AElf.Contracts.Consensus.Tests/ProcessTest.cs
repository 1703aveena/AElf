using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AElf.Common;
using AElf.Contracts.Consensus.DPoS.Extensions;
using AElf.Contracts.Token;
using AElf.Cryptography;
using AElf.Cryptography.ECDSA;
using AElf.Kernel;
using Xunit;

namespace AElf.Contracts.Consensus.Tests
{
    public class ProcessTest
    {
        private int ChainId { get; set; } = ChainHelpers.ConvertBase58ToChainId("AELF");

        private Address ConsensusContractAddress { get; set; } =
            Address.BuildContractAddress(ChainHelpers.ConvertBase58ToChainId("AELF"), 1);
        
        private Address BasicContractZero { get; set; } =
            Address.BuildContractAddress(ChainHelpers.ConvertBase58ToChainId("AELF"), 0);

        [Fact]
        public async Task InitialTermTest()
        {
            var helper = new ContractTestHelper(ChainId);
            await helper.InitialChainAsync();
            
            var miners = new List<ECKeyPair>();
            for (var i = 0; i < 17; i++)
            {
                miners.Add(CryptoHelpers.GenerateKeyPair());
            }

            var initialTerm =
                new Miners {PublicKeys = {miners.Select(m => m.PublicKey.ToHex())}}.GenerateNewTerm(4000);
            var bytes = await helper.ExecuteContractAsync(ConsensusContractAddress, "InitialTerm", miners[0],
                initialTerm);

            var result = ActionResult.Parser.ParseFrom(bytes);
            
            Assert.True(result.Success);
        }

        [Fact]
        public async Task DeployContractTest()
        {
            var helper = new ContractTestHelper(ChainId);
            await helper.InitialChainAsync();

            var tx = helper.GenerateTransaction(BasicContractZero, "DeploySmartContract",
                CryptoHelpers.GenerateKeyPair(), 2,
                File.ReadAllBytes(typeof(TokenContract).Assembly.Location));

            await helper.MineABlockAsync(new List<Transaction> {tx});

            var chain = await helper.GetChainAsync();

            Assert.Equal(2UL, chain.BestChainHeight);
            
            var tokenContractAddress = Address.BuildContractAddress(ChainHelpers.ConvertBase58ToChainId("AELF"), 2);

            await helper.ExecuteContractAsync(tokenContractAddress, "Initialize",
                CryptoHelpers.GenerateKeyPair(), "ELF", "AElf Token", 1000000000, 2);
        }
    }
}