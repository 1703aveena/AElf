﻿using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

namespace AElf.Runtime.CSharp.Tests
{
    public class ContractCodeLoadContextTest
    {

        private string _apiDllDirectory = "../../../../AElf.Runtime.CSharp.Tests.TestContract/bin/Debug/netstandard2.2/";
        private string _codePath = "../../../../AElf.Runtime.CSharp.Tests.TestContract/bin/Debug/netstandard2.2/AElf.Runtime.CSharp.Tests.TestContract.dll";

        private ContractCodeLoadContext _loadContext;
        public ContractCodeLoadContextTest()
        {
            _loadContext = new ContractCodeLoadContext(System.IO.Path.GetFullPath(_apiDllDirectory), null);
        }

        [Fact]
        public void Test()
        {
            Assembly assembly = null;
            using (FileStream file = File.OpenRead(System.IO.Path.GetFullPath(_codePath)))
            {
                assembly = _loadContext.LoadFromStream(file);
            }
            var type = assembly.GetTypes().FirstOrDefault(x => x.BaseType.Name.EndsWith("CSharpSmartContract"));
            Assert.NotNull(type);
            Assert.NotNull(_loadContext.Sdk);
        }
    }
}
