﻿using System.IO;
using AElf.Kernel;
using AElf.SmartContract;
using AElf.Types.CSharp;

using Xunit;
using ServiceStack;
using AElf.Common;
using Google.Protobuf;

namespace AElf.Contracts.Genesis.Tests
{
    public sealed class ContractZeroTest : GenesisContractTestBase
    {
        private TestContractShim _contractShim;

        private IExecutive Executive { get; set; }

        private byte[] Code
        {
            get
            {
                byte[] code;
                using (var file = File.OpenRead(Path.GetFullPath("../../../../AElf.Contracts.Token/bin/Debug/netstandard2.0/AElf.Contracts.Token.dll")))
                {
                    code = file.ReadFully();
                }
                return code;
            }
        }
        private byte[] CodeNew
        {
            get
            {
                byte[] code;
                using (var file = File.OpenRead(Path.GetFullPath("../../../../AElf.Benchmark.TestContract/bin/Debug/netstandard2.0/AElf.Benchmark.TestContract.dll")))
                {
                    code = file.ReadFully();
                }
                return code;
            }
        }
        
        public ContractZeroTest()
        {
            _contractShim = GetRequiredService<TestContractShim>();
        }

        [Fact]
        public void Test()
        {
            // deploy contract
             _contractShim.DeploySmartContract(0, Code);
            Assert.NotNull(_contractShim.TransactionContext.Trace.RetVal);
            
            // get the address of deployed contract
            var address = Address.FromBytes(_contractShim.TransactionContext.Trace.RetVal.Data.DeserializeToBytes());
            
            // query owner
            _contractShim.GetContractOwner(address);
            var owner = _contractShim.TransactionContext.Trace.RetVal.Data.DeserializeToPbMessage<Address>();
            Assert.Equal(_contractShim.Sender, owner);

            // chang owner and query again, owner will be new owner
            var newOwner = Address.Generate();
            _contractShim.ChangeContractOwner(address, newOwner);
            _contractShim.GetContractOwner(address);
            var queryNewOwner = _contractShim.TransactionContext.Trace.RetVal.Data.DeserializeToPbMessage<Address>();
            Assert.Equal(newOwner, queryNewOwner);

            _contractShim.UpdateSmartContract(address, CodeNew);
            Assert.NotNull(_contractShim.TransactionContext.Trace.RetVal);
            Assert.True(_contractShim.TransactionContext.Trace.RetVal.Data.DeserializeToBool());
        }
    }
}