﻿using System;
using System.Collections.Generic;
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
            CurrentTermNumberField = new UInt64Field(GlobalConfig.AElfDPoSCurrentTermNumber),
            BlockchainStartTimestamp = new PbField<Timestamp>(GlobalConfig.AElfDPoSBlockchainStartTimestamp),
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
            AgeToRoundNumberMap = new Map<UInt64Value, UInt64Value>(GlobalConfig.AElfDPoSAgeToRoundNumberMapString)
        };

        private Process Process => new Process(Collection);

        private Election Election => new Election(Collection);
        
        private Validation Validation => new Validation(Collection);

        #region Process
        
        [Fee(0)]
        public void InitialTerm(Term term, int logLevel)
        {
            Api.Assert(term.FirstRound.RoundNumber == 1, "It seems that the term number of initial term is incorrect.");
            Api.Assert(term.SecondRound.RoundNumber == 2, "It seems that the term number of initial term is incorrect.");
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
            Process.PackageOutValue(toPackage);
        }

        [Fee(0)]
        public void BroadcastInValue(ToBroadcast toBroadcast)
        {
            Process.BroadcastInValue(toBroadcast);
        }
        
        #endregion Process

        #region Query
        
        [View]
        public Round GetRoundInfo(ulong roundNumber)
        {
            if (Collection.RoundsMap.TryGet(roundNumber.ToUInt64Value(), out var roundInfo))
            {
                return roundInfo;
            }
            
            return new Round
            {
                Remark = "Round information not found."
            };
        }

        [View]
        public ulong GetCurrentRoundNumber()
        {
            return Collection.CurrentRoundNumberField.GetValue();
        }
        
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
        public StringList GetCandidatesList()
        {
            return Collection.CandidatesField.GetValue().PublicKeys.ToList().ToStringList();
        }
        
        [View]
        public string GetCandidatesListToFriendlyString()
        {
            return GetCandidatesList().ToString();
        }

        [View]
        public CandidateInHistory GetCandidateHistoryInfo(string publicKey)
        {
            if (Collection.HistoryMap.TryGet(publicKey.ToStringValue(), out var info))
            {
                return info;
            }

            return new CandidateInHistory
            {
                PublicKey = publicKey,
                ContinualAppointmentCount = 0,
                MissedTimeSlots = 0,
                ProducedBlocks = 0,
                ReappointmentCount = 0
            };
        }

        [View]
        public string GetCandidateHistoryInfoToFriendlyString(string publicKey)
        {
            return GetCandidateHistoryInfo(publicKey).ToString();
        }
        
        [View]
        public CandidateInHistoryDictionary GetCandidatesHistoryInfo()
        {
            var result = new CandidateInHistoryDictionary();
            
            var candidates = Collection.CandidatesField.GetValue();
            result.CandidatesNumber = candidates.PublicKeys.Count;
            
            var age = Collection.AgeField.GetValue();
            foreach (var candidate in candidates.PublicKeys)
            {
                if (Collection.HistoryMap.TryGet(candidate.ToStringValue(), out var info))
                {
                    if (Collection.TicketsMap.TryGet(candidate.ToStringValue(), out var tickets))
                    {
                        var number = tickets.VotingRecords.Where(vr => vr.To == candidate && !vr.IsExpired(age))
                            .Aggregate<VotingRecord, ulong>(0, (current, ticket) => current + ticket.Count);

                        info.CurrentVotesNumber = number;
                        result.Maps.Add(candidate, info);
                    }
                }
            }

            return result;
        }

        [View]
        public string GetCandidatesHistoryInfoToFriendlyString()
        {
            return GetCandidatesHistoryInfo().ToString();
        }
        
        [View]
        public CandidateInHistoryDictionary GetPageableCandidatesHistoryInfo(int startIndex, int length)
        {
            var result = new CandidateInHistoryDictionary();

            var candidates = Collection.CandidatesField.GetValue();
            result.CandidatesNumber = candidates.PublicKeys.Count;
            
            var age = Collection.AgeField.GetValue();

            var take = Math.Min(result.CandidatesNumber - startIndex, length - startIndex);
            foreach (var candidate in candidates.PublicKeys.Skip(startIndex).Take(take))
            {
                if (Collection.HistoryMap.TryGet(candidate.ToStringValue(), out var info))
                {
                    if (Collection.TicketsMap.TryGet(candidate.ToStringValue(), out var tickets))
                    {
                        var number = tickets.VotingRecords.Where(vr => vr.To == candidate && !vr.IsExpired(age))
                            .Aggregate<VotingRecord, ulong>(0, (current, ticket) => current + ticket.Count);

                        info.CurrentVotesNumber = number;
                        result.Maps.Add(candidate, info);
                    }
                }
            }

            return result;
        }
        
        [View]
        public string GetPageableCandidatesHistoryInfoToFriendlyString(int startIndex, int length)
        {
            return GetPageableCandidatesHistoryInfo(startIndex, length).ToString();
        }
        
        [View]
        public Miners GetCurrentMiners()
        {
            var currentTermNumber = Collection.CurrentTermNumberField.GetValue();
            if (currentTermNumber == 0)
            {
                currentTermNumber = 1;
            }

            if (Collection.MinersMap.TryGet(currentTermNumber.ToUInt64Value(), out var currentMiners))
            {
                return currentMiners;
            }
            
            return new Miners
            {
                Remark = "Can't get current miners."
            };
        }
        
        [View]
        public string GetCurrentMinersToFriendlyString()
        {
            return GetCurrentMiners().ToString();
        }

        // TODO: Add an API to get unexpired tickets info.
        [View]
        public Tickets GetTicketsInfo(string publicKey)
        {
            if (Collection.TicketsMap.TryGet(publicKey.ToStringValue(), out var tickets))
            {
                tickets.VotingRecordsCount = (ulong) tickets.VotingRecords.Count;
                return tickets;
            }

            return new Tickets
            {
                TotalTickets = 0
            };
        }
        
        [View]
        public string GetTicketsInfoToFriendlyString(string publicKey)
        {
            return GetTicketsInfo(publicKey).ToString();
        }

        [View]
        public Tickets GetPageableTicketsInfo(string publicKey, int startIndex, int length)
        {
            if (Collection.TicketsMap.TryGet(publicKey.ToStringValue(), out var tickets))
            {
                var take = Math.Min(length - startIndex, tickets.VotingRecords.Count - startIndex);
                var result = new Tickets
                {
                    TotalTickets = tickets.TotalTickets,
                    VotingRecords = { tickets.VotingRecords.Skip(startIndex).Take(take)}
                };
                result.VotingRecordsCount = (ulong) result.VotingRecords.Count;
                return result;
            }
            
            return new Tickets
            {
                TotalTickets = 0
            };
        }

        public string GetPageableTicketsInfoToFriendlyString(string publicKey, int startIndex, int length)
        {
            return GetPageableTicketsInfo(publicKey, startIndex, length).ToString();
        }
        
        /// <summary>
        /// Order by:
        /// 0 - Announcement order. (Default)
        /// 1 - Tickets count ascending.
        /// 2 - Tickets count descending.
        /// </summary>
        /// <param name="startIndex"></param>
        /// <param name="length"></param>
        /// <param name="orderBy"></param>
        /// <returns></returns>
        [View]
        public TicketsDictionary GetPageableElectionInfo(int startIndex, int length, int orderBy)
        {
            if (orderBy == 0)
            {
                var publicKeys = Collection.CandidatesField.GetValue().PublicKeys;
                if (length == 0)
                {
                    length = publicKeys.Count;
                }
                var dict = new Dictionary<string, Tickets>();
                var take = Math.Min(length - startIndex, publicKeys.Count - startIndex);
                foreach (var publicKey in publicKeys.Skip(startIndex).Take(take))
                {
                    if (Collection.TicketsMap.TryGet(publicKey.ToStringValue(), out var tickets))
                    {
                        dict.Add(publicKey, tickets);
                    }
                }

                return dict.ToTicketsDictionary();
            }

            if (orderBy == 1)
            {
                var publicKeys = Collection.CandidatesField.GetValue().PublicKeys;
                if (length == 0)
                {
                    length = publicKeys.Count;
                }
                var dict = new Dictionary<string, Tickets>();
                foreach (var publicKey in publicKeys)
                {
                    if (Collection.TicketsMap.TryGet(publicKey.ToStringValue(), out var tickets))
                    {
                        dict.Add(publicKey, tickets);
                    }
                }
                
                var take = Math.Min(length - startIndex, publicKeys.Count - startIndex);
                return dict.OrderBy(p => p.Value.TotalTickets).Skip(startIndex).Take(take).ToTicketsDictionary();
            }
            
            if (orderBy == 2)
            {
                var publicKeys = Collection.CandidatesField.GetValue().PublicKeys;
                if (length == 0)
                {
                    length = publicKeys.Count;
                }
                var dict = new Dictionary<string, Tickets>();
                foreach (var publicKey in publicKeys)
                {
                    if (Collection.TicketsMap.TryGet(publicKey.ToStringValue(), out var tickets))
                    {
                        dict.Add(publicKey, tickets);
                    }
                }

                var take = Math.Min(length - startIndex, publicKeys.Count - startIndex);
                return dict.OrderByDescending(p => p.Value.TotalTickets).Skip(startIndex).Take(take).ToTicketsDictionary();
            }

            return new TicketsDictionary
            {
                Remark = "Failed to get election information."
            };
        }
        
        [View]
        public string GetPageableElectionInfoToFriendlyString(int startIndex, int length, int orderBy)
        {
            return GetPageableElectionInfo(startIndex, length, orderBy).ToString();
        }
        
        [View]
        public ulong GetBlockchainAge()
        {
            return Collection.AgeField.GetValue();
        }

        [View]
        public StringList GetCurrentVictories()
        {
            return Process.GetVictories().ToStringList();
        }
        
        [View]
        public string GetCurrentVictoriesToFriendlyString()
        {
            return GetCurrentVictories().ToString();
        }
  
        [View]
        public TermSnapshot GetTermSnapshot(ulong termNumber)
        {
            if (Collection.SnapshotField.TryGet(termNumber.ToUInt64Value(), out var snapshot))
            {
                return snapshot;
            }
            
            return new TermSnapshot
            {
                Remark = "Invalid term number."
            };
        }
        
        [View]
        public string GetTermSnapshotToFriendlyString(ulong termNumber)
        {
            return GetTermSnapshot(termNumber).ToString();
        }

        [View]
        public string QueryAlias(string publicKey)
        {
            return Collection.AliasesMap.TryGet(new StringValue {Value = publicKey}, out var alias)
                ? alias.Value
                : publicKey.Substring(0, GlobalConfig.AliasLimit);
        }

        [View]
        public ulong GetTermNumberByRoundNumber(ulong roundNumber)
        {
            var map = Collection.TermNumberLookupField.GetValue().Map;
            Api.Assert(map != null, GlobalConfig.TermNumberLookupNotFound);
            return map?.OrderBy(p => p.Key).Last(p => roundNumber >= p.Value).Key ?? (ulong) 0;
        }
        
        [View]
        public ulong GetVotesCount()
        {
            return Collection.VotesCountField.GetValue();
        }

        [View]
        public ulong GetTicketsCount()
        {
            return Collection.TicketsCountField.GetValue();
        }

        [View]
        public ulong QueryCurrentDividendsForVoters()
        {
            return Collection.RoundsMap.TryGet(GetCurrentRoundNumber().ToUInt64Value(), out var roundInfo)
                ? Config.GetDividendsForVoters(roundInfo.GetMinedBlocks())
                : 0;
        }

        [View]
        public ulong QueryCurrentDividends()
        {
            return Collection.RoundsMap.TryGet(GetCurrentRoundNumber().ToUInt64Value(), out var roundInfo)
                ? Config.GetDividendsForAll(roundInfo.GetMinedBlocks())
                : 0;
        }

        [View]
        public StringList QueryAliasesInUse()
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

        [View]
        public ulong QueryMinedBlockCountInCurrentTerm(string publicKey)
        {
            if (Collection.RoundsMap.TryGet(GetCurrentRoundNumber().ToUInt64Value(), out var round))
            {
                if (round.RealTimeMinersInfo.ContainsKey(publicKey))
                {
                    return round.RealTimeMinersInfo[publicKey].ProducedBlocks;
                }
            }

            return 0;
        }
        
        [View]
        public string QueryAliasesInUseToFriendlyString()
        {
            return QueryAliasesInUse().ToString();
        }
        
        #endregion
        
        #region Election
        
        public void AnnounceElection(string alias)
        {
            Election.AnnounceElection(alias);
        }

        public void QuitElection()
        {
            Election.QuitElection();
        }

        public void Vote(string candidatePublicKey, ulong amount, int lockTime)
        {
            Election.Vote(candidatePublicKey, amount, lockTime);
        }

        public void ReceiveDividendsByVotingDetail(string candidatePublicKey, ulong amount, int lockDays)
        {
            Election.ReceiveDividends(candidatePublicKey, amount, lockDays);
        }

        public void ReceiveDividendsByTransactionId(Hash transactionId)
        {
            Election.ReceiveDividends(transactionId);
        }
        
        public void ReceiveAllDividends()
        {
            Election.ReceiveDividends();
        }
        
        public void WithdrawByDetail(string candidatePublicKey, ulong amount, int lockDays)
        {
            Election.Withdraw(candidatePublicKey, amount, lockDays);
        }
        
        public void WithdrawByTransactionId(Hash transactionId)
        {
            Election.Withdraw(transactionId);
        }

        public void WithdrawAll(bool withoutLimitation)
        {
            Election.Withdraw(withoutLimitation);
        }

        public void InitialBalance(Address address, ulong amount)
        {
            var sender = Api.RecoverPublicKey().ToHex();
            Api.Assert(Collection.RoundsMap.TryGet(((ulong) 1).ToUInt64Value(), out var firstRound),
                "First round not found.");
            Api.Assert(firstRound.RealTimeMinersInfo.ContainsKey(sender),
                "Sender should be one of the initial miners.");
            
            Api.SendInlineByContract(Api.TokenContractAddress, "Transfer", address, amount);
        }
        
        #endregion Election
        
        #region Validation

        public BlockValidationResult ValidateBlock(BlockAbstract blockAbstract)
        {
            return Validation.ValidateBlock(blockAbstract);
        }
        
        #endregion Validation
    }
}