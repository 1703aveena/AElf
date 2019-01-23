﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AElf.Kernel;
using AElf.Kernel.Managers;
using AElf.ChainController;
using AElf.SmartContract;
using AElf.Execution;
using AElf.Execution.Scheduling;
using AElf.Types.CSharp;
using Google.Protobuf;
using AElf.Common;
using AElf.Execution.Execution;
using AElf.Kernel.Storages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
namespace AElf.Benchmark
{
    public class Benchmarks
    {
        private readonly IChainCreationService _chainCreationService;
        private readonly ISmartContractService _smartContractService;
        public ILogger<Benchmarks> Logger {get;set;}
        private readonly BenchmarkOptions _options;
        private readonly IExecutingService _executingService;
        private readonly IStateManager _stateManager;
        private readonly IMinersManager _minersManager;

        private readonly ServicePack _servicePack;

        private readonly TransactionDataGenerator _dataGenerater;
        private readonly Address _contractHash;
        
        private int ChainId { get; }
        private int _incrementId;
        
        public byte[] SmartContractZeroCode
        {
            get
            {
                byte[] code = File.ReadAllBytes(Path.GetFullPath(Path.GetFullPath(Path.Combine(_options.DllDir, _options.ZeroContractDll))));

                return code;
            }
        }

        public Benchmarks(IStateManager stateManager, IChainCreationService chainCreationService,
            IChainContextService chainContextService, ISmartContractService smartContractService,
             IFunctionMetadataService functionMetadataService,BenchmarkOptions options, IExecutingService executingService, IMinersManager minersManager)
        {
            ChainId = Hash.LoadByteArray(new byte[] { 0x01, 0x02, 0x03 });
            _stateManager = stateManager;
            _chainCreationService = chainCreationService;
            _smartContractService = smartContractService;
            Logger = NullLogger<Benchmarks>.Instance;
            _options = options;
            _executingService = executingService;
            _minersManager = minersManager;


            _servicePack = new ServicePack
            {
                ChainContextService = chainContextService,
                SmartContractService = _smartContractService,
                ResourceDetectionService = new ResourceUsageDetectionService(functionMetadataService),
                StateManager = _stateManager,
                MinersManager = _minersManager
            };

            _dataGenerater = new TransactionDataGenerator(options);
            var code = File.ReadAllBytes(Path.GetFullPath(options.DllDir + "/" + options.ContractDll));
            _contractHash = Prepare(code).Result;
        }

        public async Task BenchmarkEvenGroup()
        {
            var resDict = new Dictionary<string, double>();
            try
            {
                for (int currentGroupCount = _options.GroupRange.ElementAt(0); currentGroupCount <= _options.GroupRange.ElementAt(1); currentGroupCount++)
                {
                    Logger.LogInformation($"Start executing {currentGroupCount} groups where have {_options.TxNumber} transactions in total.");
                    var res = await MultipleGroupBenchmark(_options.TxNumber, currentGroupCount);
                    resDict.Add(res.Key, res.Value);
                }

                Logger.LogInformation("Benchmark report \n \t Configuration: \n" + _options +
                             "\n\n\n\t Benchmark result:\n" + string.Join("\n", resDict.Select(kv=> "\t" + kv.Key + ": " + kv.Value)));
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Exception while benchmark event group.");
            }
        }


        public async Task<KeyValuePair<string, double>> MultipleGroupBenchmark(int txNumber, int groupCount)
        {
            int repeatTime = _options.RepeatTime;
        
            var txList = _dataGenerater.GetMultipleGroupTx(txNumber, groupCount, _contractHash);

            var targets = GetTargetHashesForTransfer(txList);
            var originBalance = await ReadBalancesForAddrs(targets, _contractHash);
            var expectedTransferBalance = originBalance.Select(balance => balance + (ulong)(((txNumber / groupCount) * 20)*repeatTime)).ToList();
            
            long timeused = 0;
            
            for (int i = 0; i < repeatTime; i++)
            {
                Logger.LogInformation($"round {i+1} / {repeatTime} start");
                foreach (var tx in txList)
                {
                    tx.IncrementId += 1;
                }
                Stopwatch swExec = new Stopwatch();
                swExec.Start();

                var cts = new CancellationTokenSource();
                var txResult = await _executingService.ExecuteAsync(txList, ChainId,DateTime.Now, cts.Token);
        
                swExec.Stop();
                timeused += swExec.ElapsedMilliseconds;
                txResult.ForEach(trace =>
                {
                    if (!trace.StdErr.IsNullOrEmpty())
                    {
                        Logger.LogError("Execution error: " + trace.StdErr);
                    }
                });
                Thread.Sleep(50); //sleep 50 ms to let async logger finish printing contents of previous round
                Logger.LogInformation($"round {i+1} / {repeatTime} ended, used time {swExec.ElapsedMilliseconds} ms");
            }
            
            var acturalBalance = await ReadBalancesForAddrs(GetTargetHashesForTransfer(txList), _contractHash);
            
            //A double zip, first combine expectedTransferBalance with acturalBalance to get the compare string " {tx count per group} * transferBal * repeatTime = {expected} || {actural}"
            //              then combine originBalance with the compare string above.
            Logger.LogInformation(
                $"Validation for balance transfer for {groupCount} group with {txNumber / groupCount} transactions: \n\t" +
                string.Join("\n\t",
                    originBalance.Zip(
                        expectedTransferBalance.Zip(
                            acturalBalance,
                            (expected, actural) => $"{txNumber / groupCount} * 20 * {repeatTime} = {expected} || actural: {actural}"),
                        (origin, compareStr) => $"expected: {origin} + {compareStr}")));


            for (int j = 0; j < acturalBalance.Count; j++)
            {
                if (expectedTransferBalance[j] != acturalBalance[j])
                {
                    throw new Exception($"Result inconsist in transaction with {groupCount} groups, see log for more details");
                }
            }
            
            var time = txNumber / (timeused / 1000.0 / repeatTime);
            var str = groupCount + " groups with " + txList.Count + " tx in total";

            return new KeyValuePair<string,double>(str, time);
        }

        private List<Address> GetTargetHashesForTransfer(List<Transaction> transactions)
        {
            if (transactions.Count(a => a.MethodName != "Transfer") != 0)
            {
                throw new Exception("Missuse for function GetTargetHashesForTransfer with non transfer transactions");
            }
            HashSet<Address> res = new HashSet<Address>();
            foreach (var tx in transactions)
            {
                var parameters = ParamsPacker.Unpack(tx.Params.ToByteArray(), new[] {typeof(Address), typeof(Address), typeof(ulong)});
                res.Add(parameters[1] as Address);
            }

            return res.ToList();
        }

        private async Task<List<ulong>> ReadBalancesForAddrs(List<Address> targets, Address tokenContractAddr)
        {
            List<ulong> res = new List<ulong>();
            foreach (var target in targets)
            {
                Transaction tx = new Transaction()
                {
                    From = target,
                    To = tokenContractAddr,
                    IncrementId = 0,
                    MethodName = "GetBalance",
                    Params = ByteString.CopyFrom(ParamsPacker.Pack(target)),
                };
                
                var txnCtxt = new TransactionContext()
                {
                    Transaction = tx
                };


                var executive = await _smartContractService.GetExecutiveAsync(tokenContractAddr, ChainId);
                try
                {
                    await executive.SetTransactionContext(txnCtxt).Apply();
                }
                finally
                {
                    await _smartContractService.PutExecutiveAsync(ChainId, tokenContractAddr, executive);    
                }

                res.Add(txnCtxt.Trace.RetVal.Data.DeserializeToUInt64());
            }

            return res;
        }
        
        public ulong NewIncrementId()
        {
            var n = Interlocked.Increment(ref _incrementId);
            return (ulong)n;
        }

        public async Task<Address> Prepare(byte[] contractCode)
        {
            //create smart contact zero
            var reg = new SmartContractRegistration
            {
                Category = 0,
                ContractBytes = ByteString.CopyFrom(SmartContractZeroCode),
                ContractHash = Hash.FromRawBytes(SmartContractZeroCode),
                SerialNumber = GlobalConfig.GenesisBasicContract
            };
            var chain = await _chainCreationService.CreateNewChainAsync(ChainId,
                new List<SmartContractRegistration> {reg});
            var contractAddressZero = ContractHelpers.GetGenesisBasicContractAddress(ChainId);
            
            
            //deploy token contract
            var code = contractCode;
            
            var txnDep = new Transaction()
            {
                From = Address.Zero,
                To = contractAddressZero,
                IncrementId = 0,
                MethodName = "DeploySmartContract",
                Params = ByteString.CopyFrom(ParamsPacker.Pack(1, code))
            };
            
            var txnCtxt = new TransactionContext()
            {
                Transaction = txnDep
            };
            
            var executive = await _smartContractService.GetExecutiveAsync(contractAddressZero, ChainId);
            try
            {
                await executive.SetTransactionContext(txnCtxt).Apply();
                await txnCtxt.Trace.SmartCommitChangesAsync(_stateManager);
            }
            finally
            {
                await _smartContractService.PutExecutiveAsync(ChainId, contractAddressZero, executive);    
            }
            
            var contractAddr = txnCtxt.Trace.RetVal.Data.DeserializeToPbMessage<Address>();
            return contractAddr;
        }

        public async Task InitContract()
        {
            await InitContract(_contractHash, _dataGenerater.KeyList);
        }
        
        public async Task InitContract(Address contractAddr, IEnumerable<Address> addrBook)
        {
            //init contract
            string name = "TestToken" + _incrementId;
            var txnInit = new Transaction
            {
                From = Address.Zero,
                To = contractAddr,
                IncrementId = NewIncrementId(),
                MethodName = "InitBalance",
                Params = ByteString.CopyFrom(ParamsPacker.Pack(name, Address.Zero))
            };
            
            var txnInitCtxt = new TransactionContext()
            {
                Transaction = txnInit
            };
            var executiveUser = await _smartContractService.GetExecutiveAsync(contractAddr, ChainId);
            try
            {
                await executiveUser.SetTransactionContext(txnInitCtxt).Apply();
                await txnInitCtxt.Trace.SmartCommitChangesAsync(_stateManager);
            }
            finally
            {
                await _smartContractService.PutExecutiveAsync(ChainId, contractAddr, executiveUser);    
            }
            //init contract
            var initTxList = new List<Transaction>();
            foreach (var addr in addrBook)
            {
                var txnBalInit = new Transaction
                {
                    From = addr,
                    To = contractAddr,
                    IncrementId = NewIncrementId(),
                    MethodName = "InitBalance",
                    Params = ByteString.CopyFrom(ParamsPacker.Pack(addr))
                };
                
                initTxList.Add(txnBalInit);
            }
            var cts = new CancellationTokenSource();
            var txTrace = await _executingService.ExecuteAsync(initTxList, ChainId,DateTime.Now, cts.Token);
            foreach (var trace in txTrace)
            {
                if (!trace.StdErr.IsNullOrEmpty())
                {
                    Logger.LogError("Execution Error: " + trace.StdErr);
                }
            }
        }
        
    }
}