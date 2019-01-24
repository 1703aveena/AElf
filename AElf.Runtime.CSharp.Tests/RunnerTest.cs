﻿using System.Collections.Generic;
using AElf.Kernel;
using Xunit;
using Xunit.Frameworks.Autofac;
using AElf.Common;
using Google.Protobuf;
using Volo.Abp.DependencyInjection;

namespace AElf.Runtime.CSharp.Tests
{
    public sealed class RunnerTest : CSharpRuntimeTestBase
    {
        private MockSetup _mock;
        private TestContractShim _contract1;
        private TestContractShim _contract2;

        public RunnerTest()
        {
            _mock = GetRequiredService<MockSetup>();
            _contract1 = new TestContractShim(_mock);
            _contract2 = new TestContractShim(_mock, true);
        }

        [Fact]
        public void Test()
        {
            Address account0 = Address.Generate();
            Address account1 = Address.Generate();

            // Initialize
            _contract1.Initialize(account0, 200);
            _contract1.Initialize(account1, 100);
            _contract2.Initialize(account0, 200);
            _contract2.Initialize(account1, 100);

            // Transfer
            _contract1.Transfer(account0, account1, 10);
            _contract2.Transfer(account0, account1, 20);

            // Check balance
            var bal10 = _contract1.GetBalance(account0);
            var bal20 = _contract2.GetBalance(account0);
            var bal11 = _contract1.GetBalance(account1);
            var bal21 = _contract2.GetBalance(account1);

            Assert.Equal((ulong) 190, bal10);
            Assert.Equal((ulong) 180, bal20);

            Assert.Equal((ulong) 110, bal11);
            Assert.Equal((ulong) 120, bal21);
        }

        [Fact]
        public void CodeCheckTest()
        {
            var bl1 = new List<string>
            {
                @"System\.Reflection\..*",
                @"System\.IO\..*",
                @"System\.Net\..*",
                @"System\.Threading\..*",
            };
            
            var runner1 = new SmartContractRunner(_mock.SdkDir,bl1,new List<string>());
            runner1.CodeCheck(_mock.ContractCode, true);

            var bl2 = new List<string>
            {
                @".*"
            };
                        
            var runner2 = new SmartContractRunner(_mock.SdkDir,bl2,new List<string>());
            Assert.Throws<InvalidCodeException>(()=>runner2.CodeCheck(_mock.ContractCode, true));
        }
    }
}