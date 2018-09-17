﻿// ReSharper disable InconsistentNaming
// ReSharper disable once CheckNamespace
namespace AElf.Kernel
{
    public static class Globals
    {
        public static readonly string GenesisSmartContractZeroAssemblyName = "AElf.Contracts.Genesis";
        public static readonly string GenesisConsensusContractAssemblyName = "AElf.Contracts.Consensus";
        public static readonly string GenesisTokenContractAssemblyName = "AElf.Contracts.Token";

        public static Hash CurrentChainId = Hash.Default;
        public static readonly ulong ReferenceBlockValidPeriod = 64;

        public static readonly string GenesisBasicContract = SmartContractType.BasicContractZero.ToString();
        
        public static readonly string ConsensusContract = SmartContractType.AElfDPoS.ToString();
        
        public static int BlockProducerNumber = 17;
        public static readonly int BlockNumberOfEachRound = BlockProducerNumber + 1;
        public const int AElfLogInterval = 900;

        #region AElf DPoS

        public static bool IsConsensusGenerator;
        public const int AElfDPoSLogRoundCount = 1;
        public static int AElfDPoSMiningInterval = 4000;
        public static readonly int AElfMiningInterval = AElfDPoSMiningInterval * 9 / 10;
        public const int AElfWaitFirstRoundTime = 8000;
        public const string AElfDPoSCurrentRoundNumber = "AElfCurrentRoundNumber";
        public const string AElfDPoSBlockProducerString = "AElfBlockProducer";
        public const string AElfDPoSInformationString = "AElfDPoSInformation";
        public const string AElfDPoSExtraBlockProducerString = "AElfExtraBlockProducer";
        public const string AElfDPoSExtraBlockTimeSlotString = "AElfExtraBlockTimeSlot";
        public const string AElfDPoSFirstPlaceOfEachRoundString = "AElfFirstPlaceOfEachRound";
        public const string AElfDPoSMiningIntervalString = "AElfDPoSMiningInterval";

        #endregion

        #region PoTC

        public static ulong ExpectedTransactionCount = 8000;

        #endregion

        #region Single node test

        public static int SingleNodeTestMiningInterval = 4000;

        #endregion
    }
}