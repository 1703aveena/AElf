﻿using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AElf.Kernel;
using AElf.Kernel.Concurrency.Metadata;
using AElf.Kernel.Extensions;
using AElf.Kernel.KernelAccount;
using AElf.Sdk.CSharp;
using Google.Protobuf;
using CSharpSmartContract = AElf.Sdk.CSharp.CSharpSmartContract;

namespace AElf.Kernel.Tests
{
    public class TestContractZero : CSharpSmartContract, ISmartContractZero
    {
        [SmartContractFieldData("${this}._lock", DataAccessMode.ReadWriteAccountSharing)]
        private object _lock;
        public override async Task InvokeAsync()
        {
            await Task.CompletedTask;
        }

        [SmartContractFunction("${this}.DeploySmartContract", new string[]{}, new string[]{"${this}._lock"})]
        public async Task<Hash> DeploySmartContract(int category, byte[] contract)
        {
            Console.WriteLine("catagory: " + category + " code size " + contract.Length);
            SmartContractRegistration registration = new SmartContractRegistration
            {
                Category = category,
                ContractBytes = ByteString.CopyFrom(contract),
                ContractHash = contract.CalculateHash() // maybe no usage  
            };
            
            var tx = Api.GetTransaction();
            
            // calculate new account address
            var account = Path.CalculateAccountAddress(tx.From, tx.IncrementId);
            
            await Api.DeployContractAsync(account, registration);
            Console.WriteLine("Deployment success");
            return account;
        }

        public void Print(string name)
        {
            Console.WriteLine("Hello, " + name);
        }
        
    }
}
