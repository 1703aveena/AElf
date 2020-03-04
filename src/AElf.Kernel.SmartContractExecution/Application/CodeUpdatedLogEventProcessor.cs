using System.Threading.Tasks;
using Acs0;
using AElf.Kernel.SmartContract.Application;
using AElf.Sdk.CSharp;
using AElf.Types;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AElf.Kernel.SmartContractExecution.Application
{
    public class CodeUpdatedLogEventProcessor : IBlockAcceptedLogEventProcessor
    {
        private readonly ISmartContractAddressService _smartContractAddressService;
        private readonly ISmartContractCodeHashProvider _smartContractCodeHashProvider;

        private LogEvent _interestedEvent;

        public ILogger<CodeUpdatedLogEventProcessor> Logger { get; set; }

        public LogEvent InterestedEvent
        {
            get
            {
                if (_interestedEvent != null)
                    return _interestedEvent;

                var address = _smartContractAddressService.GetZeroSmartContractAddress();

                _interestedEvent = new CodeUpdated().ToLogEvent(address);

                return _interestedEvent;
            }
        }

        public CodeUpdatedLogEventProcessor(ISmartContractAddressService smartContractAddressService, 
            ISmartContractCodeHashProvider smartContractCodeHashProvider)
        {
            _smartContractAddressService = smartContractAddressService;
            _smartContractCodeHashProvider = smartContractCodeHashProvider;

            Logger = NullLogger<CodeUpdatedLogEventProcessor>.Instance;
        }

        public async Task ProcessAsync(Block block, TransactionResult transactionResult, LogEvent logEvent)
        {
            var eventData = new CodeUpdated();
            eventData.MergeFrom(logEvent);

            await _smartContractCodeHashProvider.SetSmartContractCodeHashAsync(block.GetHash(), eventData.Address,
                eventData.NewCodeHash);
            Logger.LogDebug($"Updated contract {eventData}");
        }
    }
}