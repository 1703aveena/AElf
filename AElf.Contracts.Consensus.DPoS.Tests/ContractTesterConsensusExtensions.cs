using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf.Common;
using AElf.Contracts.TestBase;
using AElf.Kernel;
using AElf.Kernel.Consensus.Application;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;

namespace AElf.Contracts.Consensus.DPoS.Tests
{
    public static class ContractTesterConsensusExtensions
    {
        public static async Task<ConsensusCommand> GetConsensusCommand(this ContractTester tester)
        {
            var firstExtraInformation = new DPoSTriggerInformation
            {
                Timestamp = DateTime.UtcNow.ToTimestamp(),
                PublicKey = tester.CallOwnerKeyPair.PublicKey.ToHex(),
                IsBootMiner = true,
            };
            var bytes = await tester.CallContractMethodAsync(
                tester.DeployedContractsAddresses[1], // Usually the second contract is consensus contract.
                ConsensusConsts.GetConsensusCommand,
                firstExtraInformation.ToByteArray());
            return ConsensusCommand.Parser.ParseFrom(bytes);
        }

        public static async Task<DPoSInformation> GetNewConsensusInformation(this ContractTester tester,
            DPoSTriggerInformation triggerInformation)
        {
            var bytes = await tester.CallContractMethodAsync(tester.DeployedContractsAddresses[1],
                ConsensusConsts.GetNewConsensusInformation, triggerInformation.ToByteArray());
            return DPoSInformation.Parser.ParseFrom(bytes);
        }

        public static async Task<List<Transaction>> GenerateConsensusTransactions(this ContractTester tester,
            DPoSTriggerInformation triggerInformation)
        {
            var bytes = await tester.CallContractMethodAsync(tester.DeployedContractsAddresses[1],
                ConsensusConsts.GenerateConsensusTransactions, triggerInformation.ToByteArray());
            var txs = TransactionList.Parser.ParseFrom(bytes).Transactions.ToList();
            tester.SignTransaction(ref txs, tester.CallOwnerKeyPair);
            return txs;
        }

        public static async Task<ValidationResult> ValidateConsensus(this ContractTester tester,
            DPoSInformation information)
        {
            var bytes = await tester.CallContractMethodAsync(tester.DeployedContractsAddresses[1],
                ConsensusConsts.ValidateConsensus, information.ToByteArray());
            return ValidationResult.Parser.ParseFrom(bytes);
        }

        public static async Task<Block> GenerateConsensusTransactionsAndMineABlock(this ContractTester tester,
            DPoSTriggerInformation triggerInformation, params ContractTester[] testersToExecuteBlock)
        {
            var bytes = await tester.CallContractMethodAsync(tester.DeployedContractsAddresses[1],
                ConsensusConsts.GenerateConsensusTransactions,
                triggerInformation.ToByteArray());
            var systemTxs = TransactionList.Parser.ParseFrom(bytes).Transactions.ToList();
            tester.SignTransaction(ref systemTxs, tester.CallOwnerKeyPair);

            var block = await tester.MineABlockAsync(new List<Transaction>(), systemTxs);
            foreach (var contractTester in testersToExecuteBlock)
            {
                await contractTester.ExecuteBlock(block, new List<Transaction>(), systemTxs);
            }

            return block;
        }
    }
}