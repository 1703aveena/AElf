using System.Collections.Generic;
using AElf.Common;
using AElf.Kernel;

namespace AElf.Consensus
{
    public class ConsensusTransactionGenerator : ISystemTransactionGenerator
    {
        private readonly IConsensusService _consensusService;

        public ConsensusTransactionGenerator(IConsensusService consensusService)
        {
            _consensusService = consensusService;
        }
        
        public void GenerateTransactions(Address from, ulong preBlockHeight, ulong refBlockHeight, byte[] refBlockPrefix,
            int chainId, ref List<Transaction> generatedTransactions)
        {
            generatedTransactions.AddRange(
                _consensusService.GenerateConsensusTransactionsAsync(chainId, refBlockHeight, refBlockPrefix).Result);
        }
    }
}