﻿using AElf.Kernel;
using AElf.Sdk.CSharp.Types;
using Google.Protobuf.WellKnownTypes;

namespace AElf.Contracts.Consensus.Contracts
{
    // ReSharper disable InconsistentNaming
    public class DataCollection
    {
        /// <summary>
        /// Current round number.
        /// </summary>
        public UInt64Field CurrentRoundNumberField;
        
        /// <summary>
        /// Current term number.
        /// </summary>
        public UInt64Field CurrentTermNumberField;

        /// <summary>
        /// Record the start round of each term.
        /// In Map: term number -> round number
        /// </summary>
        public PbField<TermNumberLookUp> TermNumberLookupField;

        /// <summary>
        /// Timestamp for genesis of this blockchain.
        /// </summary>
        public PbField<Timestamp> BlockchainStartTimestamp;

        /// <summary>
        /// The nodes declared join the election for Miners.
        /// </summary>
        public PbField<Candidates> CandidatesField;

        /// <summary>
        /// Days since we started this blockchain.
        /// </summary>
        public UInt64Field AgeField;
        
        /// <summary>
        /// DPoS information of each round.
        /// round number -> round information
        /// </summary>
        public Map<UInt64Value, Round> RoundsMap;
        
        /// <summary>
        /// DPoS mining interval.
        /// </summary>
        public Int32Field MiningIntervalField;
        
        /// <summary>
        /// Miners of each term.
        /// term number -> miners
        /// </summary>
        public Map<UInt64Value, Miners> MinersMap;

        /// <summary>
        /// Tickets of each address (public key).
        /// public key hex value -> tickets information
        /// </summary>
        public Map<StringValue, Tickets> TicketsMap;

        /// <summary>
        /// Snapshots of all terms.
        /// term number -> snapshot
        /// </summary>
        public Map<UInt64Value, TermSnapshot> SnapshotField;

        /// <summary>
        /// Aliases of candidates.
        /// candidate public key hex value -> alias
        /// </summary>
        public Map<StringValue, StringValue> AliasesMap;

        /// <summary>
        /// Aliases of candidates.
        /// alias -> candidate public key hex value
        /// </summary>
        public Map<StringValue, StringValue> AliasesLookupMap;
        
        /// <summary>
        /// Histories of all candidates
        /// candidate public key hex value -> history information
        /// </summary>
        public Map<StringValue, CandidateInHistory> HistoryMap;
        
        /// <summary>
        /// blockchain age -> first round number.
        /// </summary>
        public Map<UInt64Value, UInt64Value> AgeToRoundNumberMap;

        /// <summary>
        /// Keep tracking of the count of votes.
        /// </summary>
        public UInt64Field VotesCountField;
        
        /// <summary>
        /// Keep tracking of the count of tickets.
        /// </summary>
        public UInt64Field TicketsCountField;

        /// <summary>
        /// Whether 2/3 of miners mined in current term.
        /// </summary>
        public BoolField TwoThirdsMinersMinedCurrentTermField;
    }
}