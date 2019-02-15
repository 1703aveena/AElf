using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using AElf.Kernel;
using AElf.Kernel.Services;
using AElf.Management.Interfaces;
using Volo.Abp.EventBus;
using Volo.Abp.EventBus.Local;

namespace AElf.Consensus.DPoS
{
    // ReSharper disable once InconsistentNaming
    public class DPoSObserver : IConsensusObserver
    {
        private readonly IMinerService _minerService;
        private readonly INetworkService _networkService;

        public IEventBus EventBus { get; set; }

        public List<Transaction> TransactionsForBroadcasting { get; set; } = new List<Transaction>();

        public DPoSObserver(IMinerService minerService, INetworkService networkService)
        {
            _minerService = minerService;
            _networkService = networkService;
            
            EventBus = NullLocalEventBus.Instance;
        }
        
        public IDisposable Subscribe(byte[] consensusCommand)
        {
            var command = DPoSCommand.Parser.ParseFrom(consensusCommand);
            
            if (command.Behaviour == DPoSBehaviour.PublishInValue)
            {
                return Observable.Timer(TimeSpan.FromMilliseconds(command.CountingMilliseconds))
                    .Select(_ => ConsensusPerformanceType.BroadcastTransaction).Subscribe(this);
            }

            return Observable.Timer(TimeSpan.FromMilliseconds(command.CountingMilliseconds))
                .Select(_ => ConsensusPerformanceType.MineBlock).Subscribe(this);
        }

        public void OnCompleted()
        {
            throw new NotImplementedException();
        }

        public void OnError(Exception error)
        {
            throw new NotImplementedException();
        }

        public void OnNext(ConsensusPerformanceType value)
        {
            switch (value)
            {
                case ConsensusPerformanceType.MineBlock:
                    EventBus.PublishAsync(new MineBlock());
                    break;
                
                case ConsensusPerformanceType.BroadcastTransaction:
                    EventBus.PublishAsync(new BroadcastTransaction
                    {
                        Transactions = TransactionsForBroadcasting
                    });
                    break;
                
                default:
                    throw new ArgumentOutOfRangeException(nameof(value), value, null);
            }
        }
    }
}