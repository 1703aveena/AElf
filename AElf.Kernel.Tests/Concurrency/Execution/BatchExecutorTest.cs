﻿using System;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using Xunit;
using Xunit.Frameworks.Autofac;
using Akka.Actor;
using Akka.TestKit;
using Akka.TestKit.Xunit;
using Google.Protobuf;
using AElf.Kernel.Concurrency.Execution;
using AElf.Kernel.Concurrency.Execution.Messages;

namespace AElf.Kernel.Tests.Concurrency.Execution
{

    [UseAutofacTestFramework]
    public class BatchExecutorTest : TestKitBase
    {
        private MockSetup _mock;
        private ActorSystem sys = ActorSystem.Create("test");
        private IActorRef _serviceRouter;

        public BatchExecutorTest(MockSetup mock) : base(new XunitAssertions())
        {
            _mock = mock;
            _serviceRouter = sys.ActorOf(LocalServicesProvider.Props(_mock.ServicePack));
        }

        [Fact(Skip = "todo")]
        public void TwoJobBatchExecutionTest()
        {
            TwoJobBatchExecutionTestWithChildType(BatchExecutor.ChildType.Job);
            TwoJobBatchExecutionTestWithChildType(BatchExecutor.ChildType.Group);
        }

        public void TwoJobBatchExecutionTestWithChildType(BatchExecutor.ChildType childType)
        {
            /*
			 *  Job 1: (0-1, 10), (1-2, 9)
			 *  Job 2: (3-4, 8)
			 */

            var balances = new List<ulong>()
            {
                100, 0, 0, 200, 0
            };
            var addresses = Enumerable.Range(0, balances.Count).Select(x => Hash.Generate()).ToList();

            foreach (var addbal in addresses.Zip(balances, Tuple.Create))
            {
                _mock.Initialize1(addbal.Item1, addbal.Item2);
            }

            var txs = new List<ITransaction>(){
                _mock.GetTransferTxn1(addresses[0], addresses[1], 10),
                _mock.GetTransferTxn1(addresses[1], addresses[2], 9),
                _mock.GetTransferTxn1(addresses[3], addresses[4], 8)
            };
           var txsHashes = txs.Select(y => y.GetHash()).ToList();

            var finalBalances = new List<ulong>
            {
                90, 1, 9, 192, 8
            };

            var executor1 = sys.ActorOf(BatchExecutor.Props(_mock.ChainId1, _serviceRouter, txs, TestActor, childType));
            Watch(executor1);
            executor1.Tell(StartExecutionMessage.Instance);
            var traces = new List<TransactionTrace>()
            {
                ExpectMsg<TransactionTraceMessage>().TransactionTrace,
                ExpectMsg<TransactionTraceMessage>().TransactionTrace,
                ExpectMsg<TransactionTraceMessage>().TransactionTrace,
            }.OrderBy(y => txsHashes.IndexOf(y.TransactionId)).ToList();
            ExpectTerminated(executor1);

            Assert.True(string.IsNullOrEmpty(traces[0].StdErr));
            Assert.True(string.IsNullOrEmpty(traces[1].StdErr));
            Assert.True(string.IsNullOrEmpty(traces[2].StdErr));
            
            // Job 1: Tx0 -> Tx1 (Tx1 starts after Tx0 finishes)
            // Job 2: Tx2 (Tx2 starts before Tx1 finishes, not strict, but should be)
            Assert.True(_mock.GetTransactionStartTime1(txs[1]) > _mock.GetTransactionEndTime1(txs[0]));
            // TODO: Improve this
            Assert.True(_mock.GetTransactionStartTime1(txs[2]) < _mock.GetTransactionEndTime1(txs[1]));
            var actualBalances = addresses.Select(address => _mock.GetBalance1(address));
            Assert.Equal(string.Join(" ", finalBalances), string.Join(" ", actualBalances));
        }
    }
}
