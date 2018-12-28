﻿using System.Collections.Generic;
using System.Linq;
using AElf.Kernel;
using AElf.Sdk.CSharp;
using AElf.Sdk.CSharp.Types;
using Google.Protobuf.WellKnownTypes;
using AElf.Common;
using AElf.Contracts.Consensus.Contracts;
using Api = AElf.Sdk.CSharp.Api;

namespace AElf.Contracts.Consensus
{
    // ReSharper disable ClassNeverInstantiated.Global
    // ReSharper disable InconsistentNaming
    // ReSharper disable UnusedMember.Global
    public class ConsensusContract : CSharpSmartContract
    {
        private DataCollection Collection => new DataCollection
        {
            CurrentRoundNumberField = new UInt64Field(GlobalConfig.AElfDPoSCurrentRoundNumber),
            MiningIntervalField = new Int32Field(GlobalConfig.AElfDPoSMiningIntervalString),
            CandidatesField = new PbField<Candidates>(GlobalConfig.AElfDPoSCandidatesString),
            TermNumberLookupField = new PbField<TermNumberLookUp>(GlobalConfig.AElfDPoSTermNumberLookupString),
            AgeField = new UInt64Field(GlobalConfig.AElfDPoSAgeFieldString),
            CurrentTermNumberField= new UInt64Field(GlobalConfig.AElfDPoSCurrentTermNumber),
            BlockchainStartTimestamp= new PbField<Timestamp>(GlobalConfig.AElfDPoSBlockchainStartTimestamp),
            VotesCountField = new UInt64Field(GlobalConfig.AElfVotesCountString),
            TicketsCountField = new UInt64Field(GlobalConfig.AElfTicketsCountString),
            // TODO: To implement.
            TwoThirdsMinersMinedCurrentTermField = new BoolField(GlobalConfig.AElfTwoThirdsMinerMinedString),

            RoundsMap = new Map<UInt64Value, Round>(GlobalConfig.AElfDPoSRoundsMapString),
            MinersMap = new Map<UInt64Value, Miners>(GlobalConfig.AElfDPoSMinersMapString),
            TicketsMap = new Map<StringValue, Tickets>(GlobalConfig.AElfDPoSTicketsMapString),
            SnapshotField = new Map<UInt64Value, TermSnapshot>(GlobalConfig.AElfDPoSSnapshotMapString),
            AliasesMap = new Map<StringValue, StringValue>(GlobalConfig.AElfDPoSAliasesMapString),
            AliasesLookupMap = new Map<StringValue, StringValue>(GlobalConfig.AElfDPoSAliasesLookupMapString),
            HistoryMap = new Map<StringValue, CandidateInHistory>(GlobalConfig.AElfDPoSHistoryMapString),
        };

        private Process Process => new Process(Collection);

        private Election Election => new Election(Collection);

        #region Process
        
        [View]
        public Round GetRoundInfo(ulong roundNumber)
        {
            Api.Assert(Collection.RoundsMap.TryGet(roundNumber.ToUInt64Value(), out var roundInfo), GlobalConfig.RoundNumberNotFound);
            return roundInfo;
        }

        [View]
        public ulong GetCurrentRoundNumber(string empty)
        {
            return Collection.CurrentRoundNumberField.GetValue();
        }

        [Fee(0)]
        public void InitialTerm(Term term, int logLevel)
        {
            Api.Assert(term.FirstRound.RoundNumber == 1);
            Api.Assert(term.SecondRound.RoundNumber == 2);
            
            Process.InitialTerm(term, logLevel);
        }

        [Fee(0)]
        public void NextTerm(Term term)
        {
            Process.NextTerm(term);
        }

        [Fee(0)]
        public void NextRound(Forwarding forwarding)
        {
            Process.NextRound(forwarding);
        }

        [Fee(0)]
        public void PackageOutValue(ToPackage toPackage)
        {
            Process.PublishOutValue(toPackage);
        }

        [Fee(0)]
        public void BroadcastInValue(ToBroadcast toBroadcast)
        {
            Process.PublishInValue(toBroadcast);
        }
        
        #endregion

        #region Election
        
        [View]
        public ulong GetCurrentTermNumber()
        {
            return Collection.CurrentTermNumberField.GetValue();
        }

        [View]
        public bool IsCandidate(string publicKey)
        {
            return Collection.CandidatesField.GetValue().PublicKeys.Contains(publicKey);
        }
        
        [View]
        public StringList GetCandidatesList(string empty)
        {
            return Collection.CandidatesField.GetValue().PublicKeys.ToList().ToStringList();
        }

        [View]
        public CandidateInHistory GetCandidateHistoryInfo(string publicKey)
        {
            Api.Assert(Collection.HistoryMap.TryGet(publicKey.ToStringValue(), out var info),
                GlobalConfig.CandidateNotFound);
            return info;
        }

        [View]
        public Miners GetCurrentMiners()
        {
            var currentTermNumber = Collection.CurrentTermNumberField.GetValue();
            Api.Assert(Collection.MinersMap.TryGet(currentTermNumber.ToUInt64Value(), out var currentMiners),
                GlobalConfig.TermNumberNotFound);
            return currentMiners;
        }

        [View]
        public Tickets GetTicketsInfo(string publicKey)
        {
            Api.Assert(Collection.TicketsMap.TryGet(publicKey.ToStringValue(), out var tickets), GlobalConfig.TicketsNotFound);
            return tickets;
        }

        [View]
        public TicketsDictionary GetCurrentElectionInfo(string empty)
        {
            var dict = new Dictionary<string, Tickets>();
            foreach (var publicKey in Collection.CandidatesField.GetValue().PublicKeys)
            {
                if (Collection.TicketsMap.TryGet(publicKey.ToStringValue(), out var tickets))
                {
                    dict.Add(publicKey, tickets);
                }
            }

            return dict.ToTicketsDictionary();
        }
        
        [View]
        public ulong GetBlockchainAge(string empty)
        {
            return Collection.AgeField.GetValue();
        }

        [View]
        public StringList GetCurrentVictories(string empty)
        {
            return Process.GetCurrentVictories();
        }
  
        [View]
        public TermSnapshot GetTermSnapshot(ulong termNumber)
        {
            Api.Assert(Collection.SnapshotField.TryGet(termNumber.ToUInt64Value(), out var snapshot), GlobalConfig.TermSnapshotNotFound);
            return snapshot;
        }

        [View]
        public ulong GetTermNumberByRoundNumber(ulong roundNumber)
        {
            var map = Collection.TermNumberLookupField.GetValue().Map;
            Api.Assert(map != null, GlobalConfig.TermNumberLookupNotFound);
            return map?.OrderBy(p => p.Key).First(p => roundNumber >= p.Value).Key ?? (ulong) 0;
        }
        
        [View]
        public ulong GetVotesCount(string empty)
        {
            return Collection.VotesCountField.GetValue();
        }

        [View]
        public ulong GetTicketsCount(string empty)
        {
            return Collection.TicketsCountField.GetValue();
        }

        [View]
        public ulong QueryCurrentDividendsForVoters(string empty)
        {
            return Collection.RoundsMap.TryGet(GetCurrentRoundNumber(empty).ToUInt64Value(), out var roundInfo)
                ? Config.GetDividendsForVoters(roundInfo.GetMinedBlocks())
                : 0;
        }

        [View]
        public ulong QueryCurrentDividends(string empty)
        {
            return Collection.RoundsMap.TryGet(GetCurrentRoundNumber(empty).ToUInt64Value(), out var roundInfo)
                ? Config.GetDividendsForAll(roundInfo.GetMinedBlocks())
                : 0;
        }

        [View]
        public StringList QueryAliasesInUse(string empty)
        {
            var candidates = Collection.CandidatesField.GetValue();
            var result = new StringList();
            foreach (var publicKey in candidates.PublicKeys)
            {
                if (Collection.AliasesMap.TryGet(publicKey.ToStringValue(), out var alias))
                {
                    result.Values.Add(alias.Value);
                }
            }

            return result;
        }
        
        public void AnnounceElection(string alias)
        {
            Election.AnnounceElection(alias);
        }

        public void QuitElection(string empty)
        {
            Election.QuitElection();
        }

        public void Vote(string candidatePublicKey, ulong amount, int lockTime)
        {
            Election.Vote(candidatePublicKey, amount, lockTime);
        }

        public void GetDividendsByDetail(string candidatePublicKey, ulong amount, int lockDays)
        {
            Election.GetDividends(candidatePublicKey, amount, lockDays);
        }

        public void GetDividendsByTransactionId(Hash transactionId)
        {
            Election.GetDividends(transactionId);
        }
        
        public void GetAllDividends(string empty)
        {
            Election.GetDividends();
        }
        
        public void WithdrawByDetail(string candidatePublicKey, ulong amount, int lockDays)
        {
            Election.Withdraw(candidatePublicKey, amount, lockDays);
        }
        
        public void WithdrawByTransactionId(Hash transactionId)
        {
            Election.Withdraw(transactionId);
        }

        public void WithdrawAll(string empty)
        {
            Election.Withdraw();
        }
        
        #endregion
    }
}