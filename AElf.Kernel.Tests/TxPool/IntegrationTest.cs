﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AElf.Kernel.KernelAccount;
using AElf.Kernel.TxMemPool;
using Xunit;
using Xunit.Frameworks.Autofac;

namespace AElf.Kernel.Tests.TxPool
{
    [UseAutofacTestFramework]
    public class IntegrationTest
    {
        private readonly IAccountContextService _accountContextService;
        private readonly ISmartContractZero _smartContractZero;

        public IntegrationTest(IAccountContextService accountContextService, ISmartContractZero smartContractZero)
        {
            _accountContextService = accountContextService;
            _smartContractZero = smartContractZero;
        }
        
        private TxMemPool.TxPool GetPool()
        {
            return new TxMemPool.TxPool(new ChainContext(_smartContractZero, Hash.Generate()), TxPoolConfig.Default,
                _accountContextService);
        }

        [Fact]
        public async Task Start()
        {
            var pool = GetPool();
            
            var poolService = new TxPoolService(pool);
            poolService.Start();
            ulong queued = 0;
            ulong exec = 0;
            var tasks = new List<Task>();
            int k = 0;
            var threadNum = 10;
            for (var j = 0; j < 10; j++)
            {
                var task = Task.Run(async () =>
                {
                    var sortedSet = new SortedSet<ulong>();
                    var addr = Hash.Generate();
                    var i = 0;
                    while (i++ < 500)
                    {
                        var id = (ulong) new Random().Next(100);
                        sortedSet.Add(id);
                        var tx = new Transaction
                        {
                            From = addr,
                            To = Hash.Generate(),
                            IncrementId = id
                        };

                        await poolService.AddTxAsync(tx);
                    }

                    ulong c = 0;
                    foreach (var t in sortedSet)
                    {
                        if (t != c)
                            break;
                        c++;
                    }
                    
                    lock (this)
                    {
                        queued += (ulong) sortedSet.Count;
                        exec += c;
                        k++;
                    }
                });
                tasks.Add(task);
            }

            Task.WaitAll(tasks.ToArray());
            pool.QueueTxs();
            await poolService.PromoteAsync();
            Assert.Equal(k, threadNum);
            Assert.Equal(exec, poolService.GetExecutableSizeAsync().Result);
            Assert.Equal(queued - exec, poolService.GetWaitingSizeAsync().Result);
            
        }
    }
}