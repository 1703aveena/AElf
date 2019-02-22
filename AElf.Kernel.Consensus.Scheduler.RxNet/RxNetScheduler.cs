﻿using System;
using System.Reactive.Linq;
using System.Threading.Tasks;
using AElf.Kernel.Consensus.Application;
using AElf.Kernel.EventMessages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Local;

namespace AElf.Kernel.Consensus.Scheduler.RxNet
{
    public class RxNetScheduler : IConsensusScheduler, IObserver<BlockMiningEventData>, ISingletonDependency
    {
        private IDisposable _observables;

        public ILocalEventBus LocalEventBus { get; set; }

        public ILogger<RxNetScheduler> Logger { get; set; }

        public RxNetScheduler()
        {
            LocalEventBus = NullLocalEventBus.Instance;

            Logger = NullLogger<RxNetScheduler>.Instance;
        }

        public void NewEvent(int countingMilliseconds, BlockMiningEventData blockMiningEventData)
        {
            _observables = Subscribe(countingMilliseconds, blockMiningEventData);
        }

        public void CancelCurrentEvent()
        {
            _observables?.Dispose();
        }

        public void Dispose()
        {
            _observables?.Dispose();
        }

        public async Task<IDisposable> StartAsync(int chainId)
        {
            return this;
        }

        public Task StopAsync()
        {
            _observables?.Dispose();
            return Task.CompletedTask;
        }

        public IDisposable Subscribe(int countingMilliseconds, BlockMiningEventData blockMiningEventData)
        {
            Logger.LogDebug($"Will produce block after {countingMilliseconds} ms.");

            return Observable.Timer(TimeSpan.FromMilliseconds(countingMilliseconds))
                .Select(_ => blockMiningEventData).Subscribe(this);
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        // This is the callback.
        public void OnNext(BlockMiningEventData value)
        {
            Logger.LogDebug($"Published block mining event: {value}");
            LocalEventBus.PublishAsync(value);
        }
    }
}