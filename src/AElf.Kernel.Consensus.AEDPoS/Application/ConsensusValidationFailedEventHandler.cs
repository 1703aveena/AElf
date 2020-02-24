using System.Threading.Tasks;
using AElf.Kernel.Account.Application;
using AElf.Kernel.Blockchain.Application;
using AElf.Kernel.Consensus.Application;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus;

namespace AElf.Kernel.Consensus.AEDPoS.Application
{
    public class ConsensusValidationFailedEventHandler : ILocalEventHandler<ConsensusValidationFailedEventData>,
        ITransientDependency
    {
        private readonly IConsensusService _consensusService;
        private readonly IBlockchainService _blockchainService;
        private readonly IAccountService _accountService;
        public ILogger<ConsensusValidationFailedEventHandler> Logger { get; set; }

        public ConsensusValidationFailedEventHandler(IConsensusService consensusService,
            IBlockchainService blockchainService, IAccountService accountService)
        {
            _consensusService = consensusService;
            _blockchainService = blockchainService;
            _accountService = accountService;

            Logger = NullLogger<ConsensusValidationFailedEventHandler>.Instance;
        }

        public async Task HandleEventAsync(ConsensusValidationFailedEventData eventData)
        {
            var pubkey = await _accountService.GetPublicKeyAsync();
            var allowReTriggerMessage = $"Time slot already passed before execution.{pubkey.ToHex()}";
            //TODO!! should not compare error message string
            if (eventData.ValidationResultMessage == allowReTriggerMessage)
            {
                Logger.LogTrace($"Re-trigger consensus because validation failed.");
                var chain = await _blockchainService.GetChainAsync();
                await _consensusService.TriggerConsensusAsync(new ChainContext
                {
                    BlockHash = chain.BestChainHash,
                    BlockHeight = chain.BestChainHeight
                });
            }
        }
    }
}