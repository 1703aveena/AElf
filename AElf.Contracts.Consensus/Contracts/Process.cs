using System;
using System.Collections.Generic;
using System.Linq;
using AElf.Common;
using AElf.Kernel;
using Google.Protobuf.WellKnownTypes;
using Api = AElf.Sdk.CSharp.Api;

namespace AElf.Contracts.Consensus.Contracts
{
    // ReSharper disable UnusedMember.Global
    // ReSharper disable InconsistentNaming
    public class Process
    {
        private ulong CurrentRoundNumber => _collection.CurrentRoundNumberField.GetValue();

        private ulong CurrentTermNumber => _collection.CurrentTermNumberField.GetValue();

        private ulong CurrentAge => _collection.AgeField.GetValue();

        private Timestamp StartTimestamp => _collection.BlockchainStartTimestamp.GetValue();

        private int LogLevel { get; set; }

        private readonly DataCollection _collection;

        public Process(DataCollection collection)
        {
            _collection = collection;
        }

        public void InitialTerm(Term firstTerm, int logLevel)
        {
            InitialBlockchain();

            InitialMainchainToken();

            _collection.BlockchainStartTimestamp.SetValue(firstTerm.Timestamp);

            _collection.MinersMap.SetValue(firstTerm.TermNumber.ToUInt64Value(), firstTerm.Miners);

            SetAliases(firstTerm);

            firstTerm.FirstRound.RealTimeMinersInfo[Api.RecoverPublicKey().ToHex()].ProducedBlocks += 1;

            firstTerm.FirstRound.BlockchainAge = 1;
            firstTerm.SecondRound.BlockchainAge = 1;
            _collection.RoundsMap.SetValue(((ulong) 1).ToUInt64Value(), firstTerm.FirstRound);
            _collection.RoundsMap.SetValue(((ulong) 2).ToUInt64Value(), firstTerm.SecondRound);

            LogLevel = logLevel;
        }

        public ActionResult NextTerm(Term term)
        {
            // TODO: Check the miners are correct.

            // Count missed time slot of current round.
            CountMissedTimeSlots();

            Api.SendInline(Api.DividendsContractAddress, "KeepWeights", CurrentTermNumber);

            // Update current term number and current round number.
            _collection.CurrentTermNumberField.SetValue(term.TermNumber);
            _collection.CurrentRoundNumberField.SetValue(term.FirstRound.RoundNumber);

            // Reset some fields of next two rounds.
            foreach (var minerInRound in term.FirstRound.RealTimeMinersInfo.Values)
            {
                minerInRound.MissedTimeSlots = 0;
                minerInRound.ProducedBlocks = 0;
            }

            foreach (var minerInRound in term.SecondRound.RealTimeMinersInfo.Values)
            {
                minerInRound.MissedTimeSlots = 0;
                minerInRound.ProducedBlocks = 0;
            }

            // Update produced block number of this node.
            term.FirstRound.RealTimeMinersInfo[Api.RecoverPublicKey().ToHex()].ProducedBlocks += 1;

            // Update miners list.
            _collection.MinersMap.SetValue(term.TermNumber.ToUInt64Value(), term.Miners);

            // Update term number lookup. (Using term number to get first round number of related term.)
            var lookUp = _collection.TermNumberLookupField.GetValue();
            lookUp.Map[term.TermNumber] = term.FirstRound.RoundNumber;
            _collection.TermNumberLookupField.SetValue(lookUp);

            // Update blockchain age of next two rounds.
            term.FirstRound.BlockchainAge = CurrentAge;
            term.SecondRound.BlockchainAge = CurrentAge;

            // Update rounds information of next two rounds.
            _collection.RoundsMap.SetValue(CurrentRoundNumber.ToUInt64Value(), term.FirstRound);
            _collection.RoundsMap.SetValue((CurrentRoundNumber + 1).ToUInt64Value(), term.SecondRound);

            return new ActionResult {Success = true};
        }

        /// <summary>
        /// Take a snapshot of specific term.
        /// Basically this snapshot is used for getting ranks of candidates of specific term.
        /// </summary>
        /// <param name="snapshotTermNumber"></param>
        /// <param name="lastRoundNumber"></param>
        /// <returns></returns>
        public ActionResult SnapshotForTerm(ulong snapshotTermNumber, ulong lastRoundNumber)
        {
            if (_collection.SnapshotField.TryGet(snapshotTermNumber.ToUInt64Value(), out _))
            {
                return new ActionResult
                {
                    Success = false,
                    ErrorMessage = $"Snapshot of term {snapshotTermNumber} already taken."
                };
            }

            // The information of last round of provided term.
            var roundInfo = GetRoundInfo(lastRoundNumber);

            var minedBlocks = roundInfo.RealTimeMinersInfo.Values.Aggregate<MinerInRound, ulong>(0,
                (current, minerInRound) => current + minerInRound.ProducedBlocks);

            var candidateInTerms = new List<CandidateInTerm>();
            foreach (var victory in GetVictories())
            {
                if (_collection.TicketsMap.TryGet(victory.ToStringValue(), out var candidateTickets))
                {
                    candidateInTerms.Add(new CandidateInTerm
                    {
                        PublicKey = victory,
                        Votes = candidateTickets.ObtainedTickets
                    });
                }
                else
                {
                    _collection.TicketsMap.SetValue(victory.ToStringValue(), new Tickets());
                    candidateInTerms.Add(new CandidateInTerm
                    {
                        PublicKey = victory,
                        Votes = 0
                    });
                }
            }

            // Set snapshot of related term.
            _collection.SnapshotField.SetValue(snapshotTermNumber.ToUInt64Value(), new TermSnapshot
            {
                TermNumber = snapshotTermNumber,
                EndRoundNumber = CurrentRoundNumber,
                TotalBlocks = minedBlocks,
                CandidatesSnapshot = {candidateInTerms}
            });

            Console.WriteLine($"Snapshot of term {snapshotTermNumber} taken.");

            return new ActionResult {Success = true};
        }

        public ActionResult SnapshotForMiners(ulong previousTermNumber, ulong lastRoundNumber)
        {
            if (!_collection.SnapshotField.TryGet(previousTermNumber.ToUInt64Value(), out var previousTerm))
            {
                //return new ActionResult {Success = false, ErrorMessage = "Previous term snapshot not found."};
            }

            var roundInfo = GetRoundInfo(lastRoundNumber);

            CandidateInHistory candidateInHistory;
            if (previousTermNumber == 0)
            {
                // Current term is 1. Initial history information for initial miners.
                foreach (var candidate in roundInfo.RealTimeMinersInfo)
                {
                    if (_collection.HistoryMap.TryGet(candidate.Key.ToStringValue(), out _))
                    {
                        return new ActionResult
                            {Success = false, ErrorMessage = "Something wrong with getting previous term information."};
                    }

                    candidateInHistory = new CandidateInHistory
                    {
                        PublicKey = candidate.Key,
                        MissedTimeSlots = candidate.Value.MissedTimeSlots,
                        ProducedBlocks = candidate.Value.ProducedBlocks,
                        ContinualAppointmentCount = 0,
                        ReappointmentCount = 0,
                        Terms = {1}
                    };

                    _collection.HistoryMap.SetValue(candidate.Key.ToStringValue(), candidateInHistory);
                }
            }
            else
            {
                foreach (var candidate in roundInfo.RealTimeMinersInfo)
                {
                    if (_collection.HistoryMap.TryGet(candidate.Key.ToStringValue(), out var historyInfo))
                    {
                        var terms = new List<ulong>(historyInfo.Terms.ToList());

                        if (terms.Contains(previousTermNumber))
                        {
                            return new ActionResult
                                {Success = false, ErrorMessage = "Snapshot for miners in previous term already taken."};
                        }
                        
                        terms.Add(previousTermNumber);
                        
                        candidateInHistory = new CandidateInHistory
                        {
                            PublicKey = candidate.Key,
                            MissedTimeSlots = historyInfo.MissedTimeSlots + candidate.Value.MissedTimeSlots,
                            ProducedBlocks = historyInfo.ProducedBlocks + candidate.Value.ProducedBlocks,
                            ContinualAppointmentCount =
                                previousTerm.CandidatesSnapshot.Any(cit => cit.PublicKey == candidate.Key)
                                    ? historyInfo.ContinualAppointmentCount + 1
                                    : 0,
                            ReappointmentCount = historyInfo.ReappointmentCount + 1,
                            CurrentAlias = historyInfo.CurrentAlias,
                            Terms = {terms}
                        };
                    }
                    else
                    {
                        candidateInHistory = new CandidateInHistory
                        {
                            PublicKey = candidate.Key,
                            MissedTimeSlots = candidate.Value.MissedTimeSlots,
                            ProducedBlocks = candidate.Value.ProducedBlocks,
                            ContinualAppointmentCount = 0,
                            ReappointmentCount = 0,
                            Terms = {previousTermNumber}
                        };
                    }

                    _collection.HistoryMap.SetValue(candidate.Key.ToStringValue(), candidateInHistory);
                }
            }

            return new ActionResult {Success = true};
        }

        public ActionResult SendDividends(ulong dividendsTermNumber, ulong lastRoundNumber)
        {
            var roundInfo = GetRoundInfo(lastRoundNumber);

            // Set dividends of related term to Dividends Contract.
            var minedBlocks = roundInfo.RealTimeMinersInfo.Values.Aggregate<MinerInRound, ulong>(0,
                (current, minerInRound) => current + minerInRound.ProducedBlocks);
            Api.SendInline(Api.DividendsContractAddress, "AddDividends", dividendsTermNumber,
                Config.GetDividendsForVoters(minedBlocks));

            ulong totalVotes = 0;
            ulong totalReappointment = 0;
            var continualAppointmentDict = new Dictionary<string, ulong>();
            foreach (var minerInRound in roundInfo.RealTimeMinersInfo)
            {
                if (_collection.TicketsMap.TryGet(minerInRound.Key.ToStringValue(), out var candidateTickets))
                {
                    totalVotes += candidateTickets.ObtainedTickets;
                }

                if (_collection.HistoryMap.TryGet(minerInRound.Key.ToStringValue(), out var candidateInHistory))
                {
                    totalReappointment += candidateInHistory.ContinualAppointmentCount;
                    
                    continualAppointmentDict.Add(minerInRound.Key, candidateInHistory.ContinualAppointmentCount);
                }
                
                // Transfer dividends for actual miners. (The miners list based on last round of current term.)
                Api.SendDividends(
                    Address.FromPublicKey(ByteArrayHelpers.FromHexString(minerInRound.Key)),
                    Config.GetDividendsForEveryMiner(minedBlocks) +
                    (totalVotes == 0
                        ? 0
                        : Config.GetDividendsForTicketsCount(minedBlocks) * candidateTickets.ObtainedTickets / totalVotes) +
                    (totalReappointment == 0
                        ? 0
                        : Config.GetDividendsForReappointment(minedBlocks) * continualAppointmentDict[minerInRound.Key] /
                          totalReappointment));
            }

            var backups =
                _collection.CandidatesField.GetValue().PublicKeys.Except(roundInfo.RealTimeMinersInfo.Keys).ToList();
            foreach (var backup in backups)
            {
                var backupCount = (ulong) backups.Count;
                Api.SendDividends(
                    Address.FromPublicKey(ByteArrayHelpers.FromHexString(backup)),
                    backupCount == 0 ? 0 : Config.GetDividendsForBackupNodes(minedBlocks) / backupCount);
            }

            return new ActionResult {Success = true};
        }

        public void NextRound(Forwarding forwarding)
        {
            Api.Assert(
                forwarding.NextRoundInfo.RoundNumber == 0 || _collection.CurrentRoundNumberField.GetValue() <
                forwarding.NextRoundInfo.RoundNumber,
                "Incorrect round number of next round.");

            if (forwarding.NextRoundInfo.MinersHash() != GetCurrentRoundInfo().MinersHash() &&
                forwarding.NextRoundInfo.RealTimeMinersInfo.Keys.Count == GlobalConfig.BlockProducerNumber)
            {
                _collection.MinersMap.SetValue(CurrentTermNumber.ToUInt64Value(),
                    forwarding.NextRoundInfo.RealTimeMinersInfo.Keys.ToMiners());
            }

            // Update the age of this blockchain
            // TODO: Need to be checked somehow
            _collection.AgeField.SetValue(forwarding.CurrentAge);

            var forwardingCurrentRoundInfo = forwarding.CurrentRoundInfo;
            var currentRoundInfo = GetRoundInfo(forwardingCurrentRoundInfo.RoundNumber);
            Api.Assert(forwardingCurrentRoundInfo.RoundId == currentRoundInfo.RoundId, GlobalConfig.RoundIdNotMatched);

            var completeCurrentRoundInfo = SupplyCurrentRoundInfo(currentRoundInfo, forwardingCurrentRoundInfo);

            if (forwarding.NextRoundInfo.RoundNumber == 0)
            {
                if (_collection.RoundsMap.TryGet((currentRoundInfo.RoundNumber + 1).ToUInt64Value(),
                    out var nextRoundInfo))
                {
                    foreach (var minerInRound in completeCurrentRoundInfo.RealTimeMinersInfo)
                    {
                        nextRoundInfo.RealTimeMinersInfo[minerInRound.Key].MissedTimeSlots =
                            minerInRound.Value.MissedTimeSlots;
                        nextRoundInfo.RealTimeMinersInfo[minerInRound.Key].ProducedBlocks =
                            minerInRound.Value.ProducedBlocks;
                    }

                    nextRoundInfo.BlockchainAge = CurrentAge;
                    nextRoundInfo.RealTimeMinersInfo[Api.RecoverPublicKey().ToHex()].ProducedBlocks += 1;
                    _collection.RoundsMap.SetValue(nextRoundInfo.RoundNumber.ToUInt64Value(), nextRoundInfo);
                    _collection.CurrentRoundNumberField.SetValue(nextRoundInfo.RoundNumber);
                }
            }
            else
            {
                // Update missed time slots and  produced blocks for each miner.
                foreach (var minerInRound in completeCurrentRoundInfo.RealTimeMinersInfo)
                {
                    forwarding.NextRoundInfo.RealTimeMinersInfo[minerInRound.Key].MissedTimeSlots =
                        minerInRound.Value.MissedTimeSlots;
                    forwarding.NextRoundInfo.RealTimeMinersInfo[minerInRound.Key].ProducedBlocks =
                        minerInRound.Value.ProducedBlocks;
                }

                forwarding.NextRoundInfo.BlockchainAge = CurrentAge;
                forwarding.NextRoundInfo.RealTimeMinersInfo[Api.RecoverPublicKey().ToHex()].ProducedBlocks += 1;

                if (CurrentRoundNumber > GlobalConfig.ForkDetectionRoundNumber)
                {
                    foreach (var minerInRound in forwarding.NextRoundInfo.RealTimeMinersInfo)
                    {
                        minerInRound.Value.LatestMissedTimeSlots = 0;
                    }

                    var rounds = new List<Round>();
                    for (var i = CurrentRoundNumber - GlobalConfig.ForkDetectionRoundNumber + 1;
                        i <= CurrentRoundNumber;
                        i++)
                    {
                        Api.Assert(
                            _collection.RoundsMap.TryGet(i.ToUInt64Value(), out var round),
                            GlobalConfig.RoundNumberNotFound);
                        rounds.Add(round);
                    }

                    foreach (var round in rounds)
                    {
                        foreach (var minerInRound in round.RealTimeMinersInfo)
                        {
                            if (minerInRound.Value.IsMissed &&
                                forwarding.NextRoundInfo.RealTimeMinersInfo.ContainsKey(minerInRound.Key))
                            {
                                forwarding.NextRoundInfo.RealTimeMinersInfo[minerInRound.Key].LatestMissedTimeSlots +=
                                    1;
                            }

                            if (!minerInRound.Value.IsMissed &&
                                forwarding.NextRoundInfo.RealTimeMinersInfo.ContainsKey(minerInRound.Key))
                            {
                                forwarding.NextRoundInfo.RealTimeMinersInfo[minerInRound.Key].LatestMissedTimeSlots = 0;
                            }
                        }
                    }
                }

                _collection.RoundsMap.SetValue(forwarding.NextRoundInfo.RoundNumber.ToUInt64Value(),
                    forwarding.NextRoundInfo);
                _collection.CurrentRoundNumberField.SetValue(forwarding.NextRoundInfo.RoundNumber);
            }
        }

        public void PackageOutValue(ToPackage toPackage)
        {
            Api.Assert(toPackage.RoundId == GetCurrentRoundInfo().RoundId, GlobalConfig.RoundIdNotMatched);

            var roundInfo = GetCurrentRoundInfo();

            if (roundInfo.RoundNumber != 1)
            {
                roundInfo.RealTimeMinersInfo[Api.RecoverPublicKey().ToHex()].Signature = toPackage.Signature;
            }

            roundInfo.RealTimeMinersInfo[Api.RecoverPublicKey().ToHex()].OutValue = toPackage.OutValue;

            roundInfo.RealTimeMinersInfo[Api.RecoverPublicKey().ToHex()].ProducedBlocks += 1;

            _collection.RoundsMap.SetValue(CurrentRoundNumber.ToUInt64Value(), roundInfo);
        }

        public void BroadcastInValue(ToBroadcast toBroadcast)
        {
            if (toBroadcast.RoundId != GetCurrentRoundInfo().RoundId)
            {
                return;
            }

            var roundInfo = GetCurrentRoundInfo();
            Api.Assert(roundInfo.RealTimeMinersInfo[Api.RecoverPublicKey().ToHex()].OutValue != null,
                GlobalConfig.OutValueIsNull);
            Api.Assert(roundInfo.RealTimeMinersInfo[Api.RecoverPublicKey().ToHex()].Signature != null,
                GlobalConfig.SignatureIsNull);
            Api.Assert(
                roundInfo.RealTimeMinersInfo[Api.RecoverPublicKey().ToHex()].OutValue ==
                Hash.FromMessage(toBroadcast.InValue),
                GlobalConfig.InValueNotMatchToOutValue);

            roundInfo.RealTimeMinersInfo[Api.RecoverPublicKey().ToHex()].InValue = toBroadcast.InValue;

            _collection.RoundsMap.SetValue(CurrentRoundNumber.ToUInt64Value(), roundInfo);
        }

        #region Vital Steps

        private void InitialBlockchain()
        {
            _collection.CurrentTermNumberField.SetValue(1);

            _collection.CurrentRoundNumberField.SetValue(1);

            _collection.AgeField.SetValue(1);

            var lookUp = new TermNumberLookUp();
            lookUp.Map.Add(1, 1);
            _collection.TermNumberLookupField.SetValue(lookUp);
        }

        private void InitialMainchainToken()
        {
            Api.SendInline(Api.TokenContractAddress, "Initialize", "ELF", "AElf Token", GlobalConfig.TotalSupply, 2);
        }

        private void SetAliases(Term term)
        {
            var index = 0;
            foreach (var publicKey in term.Miners.PublicKeys)
            {
                if (index >= Config.Aliases.Count)
                    continue;

                var alias = Config.Aliases[index];
                _collection.AliasesMap.SetValue(new StringValue {Value = publicKey},
                    new StringValue {Value = alias});
                _collection.HistoryMap.SetValue(new StringValue {Value = publicKey},
                    new CandidateInHistory {CurrentAlias = alias});
                index++;
            }
        }

        /// <summary>
        /// Can only supply signature, out value, in value if one missed his time slot.
        /// </summary>
        /// <param name="roundInfo"></param>
        /// <param name="forwardingRoundInfo"></param>
        private Round SupplyCurrentRoundInfo(Round roundInfo, Round forwardingRoundInfo)
        {
            foreach (var suppliedMiner in forwardingRoundInfo.RealTimeMinersInfo)
            {
                if (suppliedMiner.Value.MissedTimeSlots >
                    roundInfo.RealTimeMinersInfo[suppliedMiner.Key].MissedTimeSlots
                    && roundInfo.RealTimeMinersInfo[suppliedMiner.Key].OutValue == null)
                {
                    roundInfo.RealTimeMinersInfo[suppliedMiner.Key].OutValue = suppliedMiner.Value.OutValue;
                    roundInfo.RealTimeMinersInfo[suppliedMiner.Key].InValue = suppliedMiner.Value.InValue;
                    roundInfo.RealTimeMinersInfo[suppliedMiner.Key].Signature = suppliedMiner.Value.Signature;

                    roundInfo.RealTimeMinersInfo[suppliedMiner.Key].MissedTimeSlots += 1;
                    roundInfo.RealTimeMinersInfo[suppliedMiner.Key].IsMissed = true;
                }
            }

            _collection.RoundsMap.SetValue(roundInfo.RoundNumber.ToUInt64Value(), roundInfo);

            return roundInfo;
        }

        #endregion

        private Round GetCurrentRoundInfo()
        {
            Api.Assert(_collection.RoundsMap.TryGet(CurrentRoundNumber.ToUInt64Value(), out var currentRoundInfo),
                $"Can't get information of round {CurrentRoundNumber}");

            return currentRoundInfo;
        }

        private Round GetRoundInfo(ulong roundNumber)
        {
            Api.Assert(_collection.RoundsMap.TryGet(roundNumber.ToUInt64Value(), out var roundInfo),
                $"Can't get information of round {roundNumber}");

            return roundInfo;
        }

        private bool ValidateMiners(IEnumerable<string> minersList)
        {
            return !GetVictories().Except(minersList).Any();
        }

        public IEnumerable<string> GetVictories()
        {
            var candidates = _collection.CandidatesField.GetValue();
            var ticketsMap = new Dictionary<string, ulong>();
            foreach (var candidate in candidates.PublicKeys)
            {
                ticketsMap.Add(candidate,
                    _collection.TicketsMap.TryGet(candidate.ToStringValue(), out var tickets)
                        ? tickets.ObtainedTickets
                        : 0);
            }

            return ticketsMap.OrderByDescending(tm => tm.Value).Take(GlobalConfig.BlockProducerNumber)
                .Select(tm => tm.Key)
                .ToList();
        }

        private TermSnapshot GetPreviousTerm()
        {
            if (CurrentTermNumber == 1)
            {
                return new TermSnapshot {TermNumber = 0};
            }

            if (_collection.SnapshotField.TryGet((CurrentTermNumber - 1).ToUInt64Value(), out var previousTerm))
            {
                return previousTerm;
            }

            return new TermSnapshot {TermNumber = 0};
        }

        /// <summary>
        /// Normally this process contained in NextRound method.
        /// </summary>
        private void CountMissedTimeSlots()
        {
            var currentRoundInfo = GetCurrentRoundInfo();
            foreach (var minerInRound in currentRoundInfo.RealTimeMinersInfo)
            {
                if (minerInRound.Value.OutValue == null)
                {
                    minerInRound.Value.MissedTimeSlots += 1;
                }
            }

            _collection.RoundsMap.SetValue(currentRoundInfo.RoundNumber.ToUInt64Value(), currentRoundInfo);
        }

        /// <summary>
        /// Debug level:
        /// 6 = Off
        /// 5 = Fatal
        /// 4 = Error
        /// 3 = Warn
        /// 2 = Info
        /// 1 = Debug
        /// 0 = Trace
        /// </summary>
        /// <param name="prefix"></param>
        /// <param name="log"></param>
        /// <param name="ex"></param>
        private void ConsoleWriteLine(string prefix, string log, Exception ex = null)
        {
            if (LogLevel == 6)
                return;

            Console.WriteLine(
                $"[{DateTime.UtcNow.ToLocalTime():yyyy-MM-dd HH:mm:ss.fff} - Consensus]{prefix} - {log}.");
            if (ex != null)
            {
                Console.WriteLine(ex);
            }
        }
    }
}