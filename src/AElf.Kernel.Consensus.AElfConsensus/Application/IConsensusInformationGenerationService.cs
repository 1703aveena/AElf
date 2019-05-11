using System;
using System.Threading.Tasks;
using Google.Protobuf;

namespace AElf.Kernel.Consensus.AElfConsensus.Application
{
    // TODO: Refactor.
    public interface IConsensusInformationGenerationService
    {
        IMessage GetTriggerInformation(TriggerType triggerType);

        IMessage ParseConsensusTriggerInformation(byte[] consensusTriggerInformation);

        Task<T> ExecuteContractAsync<T>(IChainContext chainContext, string consensusMethodName,
            IMessage input, DateTime dateTime) where T : class, IMessage<T>, new();

        Task<byte[]> GetInformationToUpdateConsensusAsync(ChainContext chainContext, DateTime nextMiningTime);
    }

    public enum TriggerType
    {
        ConsensusCommand,
        BlockHeaderExtraData,
        ConsensusTransactions
    }
}