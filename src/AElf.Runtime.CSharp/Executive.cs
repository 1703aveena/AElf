using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading.Tasks;
using AElf.Kernel;
using AElf.Kernel.Infrastructure;
using AElf.CSharp.Core;
using Google.Protobuf;
using AElf.Kernel.SmartContract;
using AElf.Kernel.SmartContract.Infrastructure;
using AElf.Kernel.SmartContract.Sdk;
using AElf.Sdk.CSharp;
using AElf.Types;
using Google.Protobuf.Reflection;

namespace AElf.Runtime.CSharp
{
    public class Executive : IExecutive
    {
        private Type _contractType;
        private object _contractInstance;
        private ReadOnlyDictionary<string, IServerCallHandler> _callHandlers;
        private ServerServiceDefinition _serverServiceDefinition;

        private CSharpSmartContractProxy _smartContractProxy;
        private ITransactionContext CurrentTransactionContext => _hostSmartContractBridgeContext.TransactionContext;

        private IHostSmartContractBridgeContext _hostSmartContractBridgeContext;
        private readonly IServiceContainer<IExecutivePlugin> _executivePlugins;
        private readonly ISdkStreamManager _sdkStreamManager;

        private ContractCodeLoadContext _loadContext;
        public List<ServiceDescriptor> Descriptors { get; private set; }

        public Executive(IServiceContainer<IExecutivePlugin> executivePlugins, ISdkStreamManager sdkStreamManager)
        {
            _executivePlugins = executivePlugins;
            _sdkStreamManager = sdkStreamManager;
        }
        
        private ServerServiceDefinition GetServerServiceDefinition(ContractCodeLoadContext context)
        {
            var methodInfo = context.FindContractContainer().GetMethod("BindService", 
                new[] {context.FindContractBaseType()});
            
            return methodInfo.Invoke(null, new[] {_contractInstance}) as ServerServiceDefinition;
        }

        public void Init(SmartContractRegistration reg)
        {
            var code = reg.Code.ToByteArray();
            var loadContext = new ContractCodeLoadContext(_sdkStreamManager);
            loadContext.LoadFromStream(code.ToArray());

            _contractType = loadContext.GetContractType();
            _contractInstance = Activator.CreateInstance(_contractType);
            _smartContractProxy = new CSharpSmartContractProxy(_contractInstance);
            _serverServiceDefinition = GetServerServiceDefinition(loadContext);
            _callHandlers = _serverServiceDefinition.GetCallHandlers();

            Descriptors = _serverServiceDefinition.GetDescriptors().ToList();

            _loadContext = loadContext;
        }

        public IExecutive SetHostSmartContractBridgeContext(IHostSmartContractBridgeContext smartContractBridgeContext)
        {
            _hostSmartContractBridgeContext = smartContractBridgeContext;
            _smartContractProxy.InternalInitialize(_hostSmartContractBridgeContext);
            return this;
        }

        public WeakReference Unload()
        {
            _loadContext.Unload();
            return new WeakReference(_loadContext);
        }

        public string AssemblyName()
        {
            return _loadContext.AssemblyName();
        }

        private void Cleanup()
        {
            _smartContractProxy.Cleanup();
        }

        public async Task ApplyAsync(ITransactionContext transactionContext)
        {
            try
            {
                _hostSmartContractBridgeContext.TransactionContext = transactionContext;
                if (CurrentTransactionContext.CallDepth > CurrentTransactionContext.MaxCallDepth)
                {
                    CurrentTransactionContext.Trace.ExecutionStatus = ExecutionStatus.ExceededMaxCallDepth;
                    CurrentTransactionContext.Trace.Error = "\n" + "ExceededMaxCallDepth";
                    return;
                }

                Execute();
                if (CurrentTransactionContext.CallDepth == 0)
                {
                    // Plugin should only apply to top level transaction
                    foreach (var plugin in _executivePlugins)
                    {
                        plugin.PostMain(_hostSmartContractBridgeContext, _serverServiceDefinition);
                    }
                }
            }
            finally
            {
                _hostSmartContractBridgeContext.TransactionContext = null;
            }
        }

        public void Execute()
        {
            var s = CurrentTransactionContext.Trace.StartTime = TimestampHelper.GetUtcNow().ToDateTime();
            var methodName = CurrentTransactionContext.Transaction.MethodName;

            try
            {
                if (!_callHandlers.TryGetValue(methodName, out var handler))
                {
                    throw new RuntimeException(
                        $"Failed to find handler for {methodName}. We have {_callHandlers.Count} handlers: " +
                        string.Join(", ", _callHandlers.Keys.OrderBy(k => k))
                    );
                }

                try
                {
                    var tx = CurrentTransactionContext.Transaction;
                    var retVal = handler.Execute(tx.Params.ToByteArray());
                    if (retVal != null)
                    {
                        CurrentTransactionContext.Trace.ReturnValue = ByteString.CopyFrom(retVal);
                        // TODO: Clean up ReadableReturnValue
                        CurrentTransactionContext.Trace.ReadableReturnValue = handler.ReturnBytesToString(retVal);
                    }

                    CurrentTransactionContext.Trace.ExecutionStatus = ExecutionStatus.Executed;
                }
                catch (TargetInvocationException ex)
                {
                    CurrentTransactionContext.Trace.Error += ex;
                    CurrentTransactionContext.Trace.ExecutionStatus = ExecutionStatus.ContractError;
                }
                catch (AssertionException ex)
                {
                    CurrentTransactionContext.Trace.ExecutionStatus = ExecutionStatus.ContractError;
                    CurrentTransactionContext.Trace.Error += "\n" + ex;
                }
                catch (Exception ex)
                {
                    // TODO: Simplify exception
                    CurrentTransactionContext.Trace.ExecutionStatus = ExecutionStatus.ContractError;
                    CurrentTransactionContext.Trace.Error += "\n" + ex;
                }

                if (!handler.IsView())
                {
                    var changes = _smartContractProxy.GetChanges();

                    var address = _hostSmartContractBridgeContext.Self.ToStorageKey();
                    foreach (var key in changes.Writes.Keys)
                    {
                        if (!key.StartsWith(address))
                        {
                            throw new InvalidOperationException("a contract cannot access other contracts data");
                        }
                    }

                    foreach (var key in changes.Reads.Keys)
                    {
                        if (!key.StartsWith(address))
                        {
                            throw new InvalidOperationException("a contract cannot access other contracts data");
                        }
                    }

                    if (!CurrentTransactionContext.Trace.IsSuccessful())
                    {
                        changes.Writes.Clear();
                    }

                    CurrentTransactionContext.Trace.StateSet = changes;
                }
                else
                {
                    CurrentTransactionContext.Trace.StateSet = new TransactionExecutingStateSet();
                }
            }
            catch (Exception ex)
            {
                CurrentTransactionContext.Trace.ExecutionStatus = ExecutionStatus.SystemError;
                CurrentTransactionContext.Trace.Error += ex + "\n";
            }
            finally
            {
                // TODO: Not needed
                Cleanup();
            }

            var e = CurrentTransactionContext.Trace.EndTime = TimestampHelper.GetUtcNow().ToDateTime();
            CurrentTransactionContext.Trace.Elapsed = (e - s).Ticks;
        }

        public string GetJsonStringOfParameters(string methodName, byte[] paramsBytes)
        {
            if (!_callHandlers.TryGetValue(methodName, out var handler))
            {
                return "";
            }

            return handler.InputBytesToString(paramsBytes);
        }

//        public object GetReturnValue(string methodName, byte[] bytes)
//        {
//            if (!_callHandlers.TryGetValue(methodName, out var handler))
//            {
//                return null;
//            }
//
//            return handler.ReturnBytesToObject(bytes);
//        }

        private IEnumerable<FileDescriptor> GetSelfAndDependency(FileDescriptor fileDescriptor,
            HashSet<string> known = null)
        {
            known = known ?? new HashSet<string>();
            if (known.Contains(fileDescriptor.Name))
            {
                return new List<FileDescriptor>();
            }

            var fileDescriptors = new List<FileDescriptor>();
            fileDescriptors.AddRange(fileDescriptor.Dependencies.SelectMany(x => GetSelfAndDependency(x, known)));
            fileDescriptors.Add(fileDescriptor);
            known.Add(fileDescriptor.Name);
            return fileDescriptors;
        }

        public byte[] GetFileDescriptorSet()
        {
            var descriptor = Descriptors.Last();
            var output = new FileDescriptorSet();
            output.File.AddRange(GetSelfAndDependency(descriptor.File).Select(x => x.SerializedData));
            return output.ToByteArray();
        }

        public IEnumerable<FileDescriptor> GetFileDescriptors()
        {
            var descriptor = Descriptors.Last();
            return GetSelfAndDependency(descriptor.File);
        }

        public Hash ContractHash { get; set; }
    }
}