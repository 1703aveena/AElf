﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using AElf.ChainController;
using Akka.Actor;
using ServiceStack.Text;
using AElf.Kernel;
using AElf.Configuration;
using AElf.Execution.Scheduling;
using AElf.SmartContract;
using AElf.Common;
using AElf.Execution.Execution;

namespace AElf.Execution
{
    public class ParallelTransactionExecutingService : IExecutingService
    {
        private readonly IGrouper _grouper;
        private readonly IActorEnvironment _actorEnvironment;

        // TODO: Move it to config
        public int TimeoutMilliSeconds { get; set; } = int.MaxValue;

        public ParallelTransactionExecutingService(IActorEnvironment actorEnvironment, IGrouper grouper)
        {
            _actorEnvironment = actorEnvironment;
            _grouper = grouper;
        }

        public async Task<List<TransactionTrace>> ExecuteAsync(List<Transaction> transactions, Hash chainId,
            CancellationToken token, Hash disambiguationHash=null)
        {
            token.Register(() => _actorEnvironment.Requestor.Tell(JobExecutionCancelMessage.Instance));

            List<List<Transaction>> groups;
            Dictionary<Transaction, Exception> failedTxs;

            //disable parallel module by default because it doesn't finish yet (don't support contract call)
            if (ParallelConfig.Instance.IsParallelEnable)
            {
                var groupRes = await _grouper.ProcessWithCoreCount(GroupStrategy.Limited_MaxAddMins,
                    ActorConfig.Instance.ConcurrencyLevel, chainId, transactions);
                groups = groupRes.Item1;
                failedTxs = groupRes.Item2;
            }
            else
            {
                groups = new List<List<Transaction>> {transactions};
                failedTxs = new Dictionary<Transaction, Exception>();
            }

            var tasks = groups.Select(
                txs => Task.Run(() => AttemptToSendExecutionRequest(chainId, txs, token, disambiguationHash), token)
            ).ToArray();
            var results = (await Task.WhenAll(tasks)).SelectMany(x => x).ToList();

            foreach (var failed in failedTxs)
            {
                var failedTrace = new TransactionTrace
                {
                    StdErr = "Transaction with ID/hash " + failed.Key.GetHash().DumpHex() +
                             " failed, detail message: \n" + failed.Value.Dump(),
                    TransactionId = failed.Key.GetHash(),
                    Transaction = failed.Key
                };
                results.Add(failedTrace);
            }

            return results;
        }

        private async Task<List<TransactionTrace>> AttemptToSendExecutionRequest(Hash chainId,
            List<Transaction> transactions, CancellationToken token, Hash disambiguationHash)
        {
            while (!token.IsCancellationRequested)
            {
                var tcs = new TaskCompletionSource<List<TransactionTrace>>();
                _actorEnvironment.Requestor.Tell(new LocalExecuteTransactionsMessage(chainId, transactions, tcs, disambiguationHash));
                var traces = await tcs.Task;

                if (traces.Count > 0)
                {
                    return traces;
                }

                Thread.Sleep(1);
            }

            // Cancelled
            return transactions.Select(tx => new TransactionTrace()
            {
                TransactionId = tx.GetHash(),
                Transaction = tx,
                ExecutionStatus = ExecutionStatus.Canceled,
                StdErr = "Execution Canceled"
            }).ToList();
        }

        private void CancelExecutions()
        {
            _actorEnvironment.Requestor.Tell(JobExecutionCancelMessage.Instance);
        }
    }
}