using System;
using System.Threading.Tasks;
using AElf.Kernel.Blockchain.Application;
using AElf.Kernel.Consensus;
using AElf.Kernel.Consensus.Application;
using AElf.Kernel.Miner.Application;
using AElf.Kernel.SmartContractExecution.Application;
using AElf.Sdk.CSharp;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus;
using Volo.Abp.EventBus.Local;

namespace AElf.Kernel
{
    public class ConsensusRequestMiningEventHandler : ILocalEventHandler<ConsensusRequestMiningEventData>,
        ITransientDependency
    {
        private readonly IMinerService _minerService;
        private readonly IBlockAttachService _blockAttachService;
        private readonly ITaskQueueManager _taskQueueManager;
        private readonly IBlockchainService _blockchainService;
        private readonly IConsensusService _consensusService;
        private readonly IBlockExtraDataService _blockExtraDataService;

        public ILogger<ConsensusRequestMiningEventHandler> Logger { get; set; }

        public ILocalEventBus LocalEventBus { get; set; }

        public ConsensusRequestMiningEventHandler(IServiceProvider serviceProvider)
        {
            _minerService = serviceProvider.GetService<IMinerService>();
            _blockAttachService = serviceProvider.GetService<IBlockAttachService>();
            _taskQueueManager = serviceProvider.GetService<ITaskQueueManager>();
            _blockchainService = serviceProvider.GetService<IBlockchainService>();
            _consensusService = serviceProvider.GetService<IConsensusService>();
            _blockExtraDataService = serviceProvider.GetService<IBlockExtraDataService>();

            Logger = NullLogger<ConsensusRequestMiningEventHandler>.Instance;
            LocalEventBus = NullLocalEventBus.Instance;
        }

        public async Task HandleEventAsync(ConsensusRequestMiningEventData eventData)
        {
            try
            {
                _taskQueueManager.Enqueue(async () =>
                {
                    if (eventData.BlockTime > new Timestamp {Seconds = 3600} &&
                        eventData.BlockTime + eventData.BlockExecutionTime <
                        TimestampHelper.GetUtcNow())
                    {
                        Logger.LogTrace(
                            $"Will cancel mining due to timeout: Actual mining time: {eventData.BlockTime}, " +
                            $"execution limit: {eventData.BlockExecutionTime.Milliseconds()} ms.");
                    }

                    var block = await _minerService.MineAsync(eventData.PreviousBlockHash,
                        eventData.PreviousBlockHeight,
                        eventData.BlockTime, eventData.BlockExecutionTime);

                    await _blockchainService.AddBlockAsync(block);

                    var chain = await _blockchainService.GetChainAsync();

                    var consensusExtraData =
                        _blockExtraDataService.GetExtraDataFromBlockHeader("Consensus", block.Header);

                    // TODO: Just verify the correctness of time slot is enough.
                    var isValid = await _consensusService.ValidateConsensusBeforeExecutionAsync(new ChainContext
                    {
                        BlockHash = block.Header.PreviousBlockHash,
                        BlockHeight = block.Header.Height - 1
                    }, consensusExtraData.ToByteArray());

                    if (isValid)
                    {
                        await LocalEventBus.PublishAsync(new BlockMinedEventData
                        {
                            BlockHeader = block.Header,
                            HasFork = block.Height <= chain.BestChainHeight
                        });

                        _taskQueueManager.Enqueue(async () => await _blockAttachService.AttachBlockAsync(block),
                            KernelConstants.UpdateChainQueueName);
                    }
                }, KernelConstants.ConsensusRequestMiningQueueName);
            }
            catch (Exception e)
            {
                Logger.LogError(e.ToString());
                throw;
            }
        }
    }
}