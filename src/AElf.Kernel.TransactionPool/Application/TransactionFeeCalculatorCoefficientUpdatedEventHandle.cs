using System.Linq;
using System.Threading.Tasks;
using AElf.Contracts.MultiToken;
using AElf.Kernel.SmartContract.Application;
using AElf.Kernel.SmartContractExecution.Application;
using AElf.Kernel.Token;
using AElf.Sdk.CSharp;
using AElf.Types;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AElf.Kernel.TransactionPool.Application
{
    public class TransactionFeeCalculatorCoefficientUpdatedEventHandle : IBlockAcceptedLogEventHandler
    {
        private readonly ISmartContractAddressService _smartContractAddressService;
        private readonly ICalculateTxCostStrategy _txCostStrategy;
        private readonly ICalculateCpuCostStrategy _cpuCostStrategy;
        private readonly ICalculateRamCostStrategy _ramCostStrategy;
        private readonly ICalculateNetCostStrategy _netCostStrategy;
        private readonly ICalculateStoCostStrategy _stoCostStrategy;

        private LogEvent _interestedEvent;

        private ILogger<TransactionFeeCalculatorCoefficientUpdatedEventHandle> Logger { get; set; }

        public LogEvent InterestedEvent
        {
            get
            {
                if (_interestedEvent != null)
                    return _interestedEvent;

                var address =
                    _smartContractAddressService.GetAddressByContractName(TokenSmartContractAddressNameProvider.Name);

                _interestedEvent = new NoticeUpdateCalculateFeeAlgorithm().ToLogEvent(address);

                return _interestedEvent;
            }
        }

        public TransactionFeeCalculatorCoefficientUpdatedEventHandle(
            ISmartContractAddressService smartContractAddressService,
            ICalculateTxCostStrategy txCostStrategy,
            ICalculateCpuCostStrategy cpuCostStrategy,
            ICalculateRamCostStrategy ramCostStrategy,
            ICalculateStoCostStrategy stoCostStrategy,
            ICalculateNetCostStrategy netCostStrategy)
        {
            _smartContractAddressService = smartContractAddressService;
            _txCostStrategy = txCostStrategy;
            _cpuCostStrategy = cpuCostStrategy;
            _ramCostStrategy = ramCostStrategy;
            _stoCostStrategy = stoCostStrategy;
            _netCostStrategy = netCostStrategy;
            Logger = NullLogger<TransactionFeeCalculatorCoefficientUpdatedEventHandle>.Instance;
        }

        public async Task HandleAsync(Block block, TransactionResult transactionResult, LogEvent logEvent)
        {
            var eventData = new NoticeUpdateCalculateFeeAlgorithm();
            eventData.MergeFrom(logEvent);
            var blockIndex = new BlockIndex
            {
                BlockHash = block.GetHash(),
                BlockHeight = block.Height
            };
            var chainContext = new ChainContext
            {
                BlockHash = eventData.PreBlockHash,
                BlockHeight = eventData.BlockHeight
            };
            ICalculateCostStrategy selectedStrategy = null;
            switch (eventData.Coefficient.FeeType)
            {
                case FeeTypeEnum.Tx:
                    selectedStrategy = _txCostStrategy;
                    break;
                case FeeTypeEnum.Cpu:
                    selectedStrategy = _cpuCostStrategy;
                    break;
                case FeeTypeEnum.Ram:
                    selectedStrategy = _ramCostStrategy;
                    break;
                case FeeTypeEnum.Sto:
                    selectedStrategy = _stoCostStrategy;
                    break;
                case FeeTypeEnum.Net:
                    selectedStrategy = _netCostStrategy;
                    break;
            }

            if (selectedStrategy == null)
                return;
            if(eventData.NewPieceKey > 0)
                await selectedStrategy.ChangeAlgorithmPieceKey(chainContext, blockIndex, eventData.Coefficient.PieceKey,eventData.NewPieceKey);
            else
            {
                var param = eventData.Coefficient;
                var pieceKey = param.PieceKey;
                var paramDic = param.CoefficientDic;
                await selectedStrategy.ModifyAlgorithm(chainContext, blockIndex, pieceKey, paramDic);
                
            }
        }
    }
}