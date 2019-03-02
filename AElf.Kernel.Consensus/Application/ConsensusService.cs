﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AElf.Common;
using AElf.Kernel.Account.Application;
using AElf.Kernel.Blockchain.Application;
using AElf.Kernel.Consensus.Infrastructure;
using AElf.Kernel.EventMessages;
using AElf.Kernel.SmartContractExecution.Application;
using AElf.Types.CSharp;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AElf.Kernel.Consensus.Application
{
    public class ConsensusService : IConsensusService
    {
        private readonly ITransactionExecutingService _transactionExecutingService;

        private readonly IConsensusInformationGenerationService _consensusInformationGenerationService;
        private readonly IAccountService _accountService;
        private readonly IBlockchainService _blockchainService;
        private readonly ConsensusControlInformation _consensusControlInformation;
        private readonly IConsensusScheduler _consensusScheduler;

        private byte[] _latestGeneratedConsensusInformation;

        public ILogger<ConsensusService> Logger { get; set; }

        public ConsensusService(IConsensusInformationGenerationService consensusInformationGenerationService,
            IAccountService accountService, ITransactionExecutingService transactionExecutingService,
            IConsensusScheduler consensusScheduler, IBlockchainService blockchainService,
            ConsensusControlInformation consensusControlInformation)
        {
            _consensusInformationGenerationService = consensusInformationGenerationService;
            _accountService = accountService;
            _transactionExecutingService = transactionExecutingService;
            _blockchainService = blockchainService;
            _consensusControlInformation = consensusControlInformation;
            _consensusScheduler = consensusScheduler;

            Logger = NullLogger<ConsensusService>.Instance;
        }

        public async Task TriggerConsensusAsync(int chainId)
        {
            Logger.LogInformation("Triggering consensus scheduler.");
            
            // Prepare data for executing contract.
            var address = await _accountService.GetAccountAsync();
            var chain = await _blockchainService.GetChainAsync(chainId);
            var chainContext = new ChainContext
            {
                ChainId = chainId,
                BlockHash = chain.BestChainHash,
                BlockHeight = chain.BestChainHeight
            };
            var triggerInformation = _consensusInformationGenerationService.GetTriggerInformation();
            
            // Upload the consensus command.
            var commandBytes = await ExecuteContractAsync(address, chainContext, ConsensusConsts.GetConsensusCommand,
                triggerInformation);
            _consensusControlInformation.ConsensusCommand =
                ConsensusCommand.Parser.ParseFrom(commandBytes.ToByteArray());

            // Initial consensus scheduler.
            var blockMiningEventData = new BlockMiningEventData(chainId, chain.BestChainHash, chain.BestChainHeight,
                _consensusControlInformation.ConsensusCommand.TimeoutMilliseconds);
            _consensusScheduler.CancelCurrentEvent();
            _consensusScheduler.NewEvent(_consensusControlInformation.ConsensusCommand.CountingMilliseconds,
                blockMiningEventData);
        }

        public async Task<bool> ValidateConsensusAsync(int chainId, Hash preBlockHash, ulong preBlockHeight,
            byte[] consensusExtraData)
        {
            Logger.LogInformation("Generating consensus transactions.");

            var address = await _accountService.GetAccountAsync();
            var chainContext = new ChainContext
            {
                ChainId = chainId,
                BlockHash = preBlockHash,
                BlockHeight = preBlockHeight
            };

            var validationResultBytes = await ExecuteContractAsync(address, chainContext,
                ConsensusConsts.ValidateConsensus, consensusExtraData);
            var validationResult = validationResultBytes.DeserializeToPbMessage<ValidationResult>();

            if (!validationResult.Success)
            {
                Logger.LogError($"Consensus validating failed: {validationResult.Message}");
            }

            return validationResult.Success;
        }

        public async Task<byte[]> GetNewConsensusInformationAsync(int chainId)
        {
            Logger.LogInformation("Getting new consensus information.");

            var address = await _accountService.GetAccountAsync();

            return _latestGeneratedConsensusInformation;
/*            var chain = await _blockchainService.GetChainAsync(chainId);
            var chainContext = new ChainContext
            {
                ChainId = chainId,
                BlockHash = chain.BestChainHash,
                BlockHeight = chain.BestChainHeight
            };

            var newConsensusInformation = (await ExecuteContractAsync(chainId, await _accountService.GetAccountAsync(),
                chainContext, ConsensusConsts.GetNewConsensusInformation,
                _consensusInformationGenerationService.GenerateExtraInformation())).ToByteArray();

            _latestGeneratedConsensusInformation = newConsensusInformation;

            return newConsensusInformation;*/
        }

        public async Task<IEnumerable<Transaction>> GenerateConsensusTransactionsAsync(int chainId)
        {
            Logger.LogInformation("Generating consensus transactions.");

            var address = await _accountService.GetAccountAsync();
            var chain = await _blockchainService.GetChainAsync(chainId);
            var chainContext = new ChainContext
            {
                ChainId = chainId,
                BlockHash = chain.BestChainHash,
                BlockHeight = chain.BestChainHeight
            };

            var consensusInformationBytes = await ExecuteContractAsync(address, chainContext,
                ConsensusConsts.GetNewConsensusInformation,
                _consensusInformationGenerationService.GenerateExtraInformation());
            _latestGeneratedConsensusInformation = consensusInformationBytes.ToByteArray();

            var generatedTransactions = (await ExecuteContractAsync(address,
                    chainContext, ConsensusConsts.GenerateConsensusTransactions,
                    _consensusInformationGenerationService.GenerateExtraInformationForTransaction(
                        _latestGeneratedConsensusInformation, chainId))).DeserializeToPbMessage<TransactionList>()
                .Transactions
                .ToList();

            foreach (var generatedTransaction in generatedTransactions)
            {
                generatedTransaction.RefBlockNumber = chain.BestChainHeight;
                generatedTransaction.RefBlockPrefix = ByteString.CopyFrom(chain.BestChainHash.Value.Take(4).ToArray());
            }

            return generatedTransactions;
        }

        private async Task<ByteString> ExecuteContractAsync(Address fromAddress, IChainContext chainContext,
            string consensusMethodName, params object[] objects)
        {
            var tx = new Transaction
            {
                From = fromAddress,
                To = Address.BuildContractAddress(chainContext.ChainId, 1),
                MethodName = consensusMethodName,
                Params = ByteString.CopyFrom(ParamsPacker.Pack(objects))
            };

            var executionReturnSets = await _transactionExecutingService.ExecuteAsync(chainContext,
                new List<Transaction> {tx},
                DateTime.UtcNow, new CancellationToken());
            return executionReturnSets.Last().ReturnValue;
        }
    }
}