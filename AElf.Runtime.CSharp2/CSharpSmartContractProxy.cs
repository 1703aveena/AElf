using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using AElf.Kernel;
using AElf.Kernel.Managers;
using AElf.SmartContract;

namespace AElf.Runtime.CSharp
{
    public class CSharpSmartContractProxy
    {
        private static MethodInfo GetMethedInfo(Type type, string name)
        {
            return type.GetMethod(name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
        }

        private object _instance;

        private Dictionary<string, MethodInfo> _methodInfos = new Dictionary<string, MethodInfo>();

        public CSharpSmartContractProxy(object instance)
        {
            _instance = instance;
            InitializeMethodInfos(_instance.GetType());
        }

        private void InitializeMethodInfos(Type instanceType)
        {
            _methodInfos = new[]
            {
                nameof(SetSmartContractContext), nameof(SetTransactionContext), nameof(SetStateManager),
                nameof(GetChanges), nameof(Cleanup)
            }.ToDictionary(x => x, x => GetMethedInfo(instanceType, x));
        }

        public void SetSmartContractContext(ISmartContractContext smartContractContext)
        {
            _methodInfos[nameof(SetSmartContractContext)].Invoke(_instance, new object[] {smartContractContext});
        }

        public void SetTransactionContext(ITransactionContext transactionContext)
        {
            _methodInfos[nameof(SetTransactionContext)].Invoke(_instance, new object[] {transactionContext});
        }

        public void SetStateManager(IStateManager stateManager)
        {
            Console.WriteLine($"stateManager {stateManager==null} _instance {_instance==null} _methodInfos[nameof(SetStateManager)] {_methodInfos[nameof(SetStateManager)]==null}");
            _methodInfos[nameof(SetStateManager)].Invoke(_instance, new object[] {stateManager});
        }

        public Dictionary<StatePath, StateValue> GetChanges()
        {
            return (Dictionary<StatePath, StateValue>) _methodInfos[nameof(GetChanges)]
                .Invoke(_instance, new object[0]);
        }

        internal void Cleanup()
        {
            _methodInfos[nameof(Cleanup)].Invoke(_instance, new object[0]);
        }
    }
}