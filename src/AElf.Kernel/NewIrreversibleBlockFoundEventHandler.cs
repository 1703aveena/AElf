using System.Threading.Tasks;
using AElf.Kernel.Blockchain.Application;
using AElf.Kernel.Blockchain.Events;
using AElf.Kernel.SmartContract.Application;
using AElf.Kernel.TransactionPool.Application;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus;

namespace AElf.Kernel
{
    public class NewIrreversibleBlockFoundEventHandler : ILocalEventHandler<NewIrreversibleBlockFoundEvent>,
        ITransientDependency
    {
        private readonly ITaskQueueManager _taskQueueManager;
        private readonly IBlockchainStateService _blockchainStateService;
        private readonly IBlockchainService _blockchainService;
        private readonly ISmartContractExecutiveService _smartContractExecutiveService;
        private readonly ITransactionBlockIndexService _transactionBlockIndexService;
        private readonly ITransactionInclusivenessProvider _transactionInclusivenessProvider;
        public ILogger<NewIrreversibleBlockFoundEventHandler> Logger { get; set; }

        public NewIrreversibleBlockFoundEventHandler(ITaskQueueManager taskQueueManager,
            IBlockchainStateService blockchainStateService,
            IBlockchainService blockchainService,
            ISmartContractExecutiveService smartContractExecutiveService,
            ITransactionInclusivenessProvider transactionInclusivenessProvider,
            ITransactionBlockIndexService transactionBlockIndexService)
        {
            _taskQueueManager = taskQueueManager;
            _blockchainStateService = blockchainStateService;
            _blockchainService = blockchainService;
            _smartContractExecutiveService = smartContractExecutiveService;
            _transactionBlockIndexService = transactionBlockIndexService;
            _transactionInclusivenessProvider = transactionInclusivenessProvider;
            Logger = NullLogger<NewIrreversibleBlockFoundEventHandler>.Instance;
        }

        public Task HandleEventAsync(NewIrreversibleBlockFoundEvent eventData)
        {
            _taskQueueManager.Enqueue(async () =>
            {
                await _blockchainStateService.MergeBlockStateAsync(eventData.BlockHeight,
                    eventData.BlockHash);
            }, KernelConstants.MergeBlockStateQueueName);

            _taskQueueManager.Enqueue(async () =>
            {
                // Clean chain branch
                var chain = await _blockchainService.GetChainAsync();
                var discardedBranch = await _blockchainService.GetDiscardedBranchAsync(chain);

                if (discardedBranch.BranchKeys.Count > 0 || discardedBranch.NotLinkedKeys.Count > 0)
                {
                    _taskQueueManager.Enqueue(
                        async () => { await _blockchainService.CleanChainBranchAsync(discardedBranch); },
                        KernelConstants.UpdateChainQueueName);
                }
                
                // Clean transaction block index cache
                await _transactionBlockIndexService.CleanTransactionBlockIndexCacheAsync(eventData.BlockHeight);
                
                // Clean up long unused executive
                _smartContractExecutiveService.ClearExecutive();
            }, KernelConstants.ChainCleaningQueueName);

            // If lib grows, then set it to package transactions
            _transactionInclusivenessProvider.IsTransactionPackable = true;
            
            return Task.CompletedTask;
        }
    }
}