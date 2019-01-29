using System;
using System.Linq;
using AElf.Common;
using AElf.Kernel;
using AElf.SmartContract;
using Xunit;
using AElf.Sdk.CSharp;
using AElf.Sdk.CSharp2.Tests.TestContract;

namespace AElf.Sdk.CSharp2.Tests
{
    public class ContractTest
    {
        [Fact]
        public void Test()
        {
            var addresses = new[] {"a", "b", "c"}.Select(Address.FromString).ToList();
            var stateManager = new MockStateManager();
            var contract = new TokenContract();
            contract.SetSmartContractContext(new SmartContractContext()
            {
                ContractAddress = addresses[0]
            });
            contract.SetTransactionContext(new TransactionContext()
            {
                Transaction = new Transaction()
                {
                    From = addresses[1],
                    To = addresses[0]
                }
            });

            contract.SetStateProviderFactory(new MockStateProviderFactory(stateManager));
            contract.SetContractAddress(addresses[0]);
            contract.Initialize("ELF", "ELF Token", 1000000000, 0);
            Assert.Equal("ELF", contract.Symbol());
            Assert.Equal("ELF Token", contract.TokenName());
            Assert.Equal(1000000000UL, contract.TotalSupply());
            Assert.Equal(0U, contract.Decimals());
            Assert.Equal(1000000000UL, contract.BalanceOf(addresses[1]));
            contract.Transfer(addresses[2], 99);
            Assert.Equal(1000000000UL - 99UL, contract.BalanceOf(addresses[1]));
        }
    }
}