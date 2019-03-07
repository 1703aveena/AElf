using System;
using System.Collections.Generic;
using System.Linq;
using AElf.Common;
using AElf.Contracts.Consensus.DPoS.Extensions;
using AElf.Kernel;
using AElf.Types.CSharp;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;

namespace AElf.Contracts.Consensus.DPoS
{
    public partial class ConsensusContract
    {
        public bool TryToUpdateRoundNumber(ulong roundNumber)
        {
            var oldRoundNumber = State.CurrentRoundNumberField.Value;
            if (roundNumber != 1 && oldRoundNumber + 1 != roundNumber)
            {
                return false;
            }

            State.CurrentRoundNumberField.Value = roundNumber;
            return true;
        }

        public bool TryToUpdateTermNumber(ulong termNumber)
        {
            var oldTermNumber = State.CurrentTermNumberField.Value;
            if (termNumber != 1 && oldTermNumber + 1 != termNumber)
            {
                return false;
            }

            State.CurrentTermNumberField.Value = termNumber;
            return true;
        }

        public bool TryToGetRoundNumber(out ulong roundNumber)
        {
            roundNumber = State.CurrentRoundNumberField.Value;
            return roundNumber != 0;
        }

        public bool TryToGetTermNumber(out ulong termNumber)
        {
            termNumber = State.CurrentTermNumberField.Value;
            return termNumber != 0;
        }

        public bool TryToGetCurrentRoundInformation(out Round roundInformation)
        {
            roundInformation = null;
            if (TryToGetRoundNumber(out var roundNumber))
            {
                roundInformation = State.RoundsMap[roundNumber.ToUInt64Value()];
                if (roundInformation != null)
                {
                    return true;
                }
            }

            return false;
        }

        public bool TryToGetPreviousRoundInformation(out Round roundInformation)
        {
            if (TryToGetRoundNumber(out var roundNumber))
            {
                roundInformation = State.RoundsMap[(roundNumber - 1).ToUInt64Value()];
                if (roundInformation != null)
                {
                    return true;
                }
            }

            roundInformation = new Round();
            return false;
        }

        public bool TryToGetRoundInformation(ulong roundNumber, out Round roundInformation)
        {
            roundInformation = State.RoundsMap[roundNumber.ToUInt64Value()];
            return roundInformation != null;
        }

        public bool TryToGetMiners(ulong termNumber, out Miners miners)
        {
            miners = State.MinersMap[termNumber.ToUInt64Value()];
            return miners != null;
        }

        public bool TryToGetVictories(out Miners victories)
        {
            var candidates = State.CandidatesField.Value;
            var ticketsMap = new Dictionary<string, ulong>();
            foreach (var candidatePublicKey in candidates.PublicKeys)
            {
                var tickets = State.TicketsMap[candidatePublicKey.ToStringValue()];
                if (tickets != null)
                {
                    ticketsMap.Add(candidatePublicKey, tickets.ObtainedTickets);
                }
            }

            if (ticketsMap.Keys.Count < GetProducerNumber())
            {
                victories = null;
                return false;
            }

            victories = ticketsMap.OrderByDescending(tm => tm.Value).Take(GetProducerNumber())
                .Select(tm => tm.Key)
                .ToList().ToMiners();
            return true;
        }

        public bool TryToGetMiningInterval(out int miningInterval)
        {
            miningInterval = State.MiningIntervalField.Value;
            return miningInterval > 0;
        }

        public bool TryToGetCurrentAge(out ulong blockAge)
        {
            blockAge = State.AgeField.Value;
            return blockAge > 0;
        }

        public bool TryToGetBlockchainStartTimestamp(out Timestamp timestamp)
        {
            timestamp = State.BlockchainStartTimestamp.Value;
            return timestamp != null;
        }

        public bool TryToGetMinerHistoryInformation(string publicKey, out CandidateInHistory historyInformation)
        {
            historyInformation = State.HistoryMap[publicKey.ToStringValue()];
            return historyInformation != null;
        }

        public bool TryToGetSnapshot(ulong termNumber, out TermSnapshot snapshot)
        {
            snapshot = State.SnapshotMap[termNumber.ToUInt64Value()];
            return snapshot != null;
        }

        public bool TryToGetTicketsInformation(string publicKey, out Tickets tickets)
        {
            tickets = State.TicketsMap[publicKey.ToStringValue()];
            return tickets != null;
        }

        public bool TryToGetBackups(List<string> currentMiners, out List<string> backups)
        {
            var candidates = State.CandidatesField.Value;
            backups = candidates.PublicKeys.Except(currentMiners).ToList();
            return backups.Any();
        }

        public void SetTermNumber(ulong termNumber)
        {
            State.CurrentTermNumberField.Value = termNumber;
        }

        public void SetRoundNumber(ulong roundNumber)
        {
            State.CurrentRoundNumberField.Value = roundNumber;
        }

        public void SetBlockAge(ulong blockAge)
        {
            State.AgeField.Value = blockAge;
        }

        public void SetChainId(int chainId)
        {
            State.ChainIdField.Value = chainId;
        }

        public void SetBlockchainStartTimestamp(Timestamp timestamp)
        {
            State.BlockchainStartTimestamp.Value = timestamp;
        }

        public void AddOrUpdateMinerHistoryInformation(CandidateInHistory historyInformation)
        {
            State.HistoryMap[historyInformation.PublicKey.ToStringValue()] = historyInformation;
        }

        public bool TryToAddRoundInformation(Round round)
        {
            var ri = State.RoundsMap[round.RoundNumber.ToUInt64Value()];
            if (ri != null)
            {
                return false;
            }

            State.RoundsMap[round.RoundNumber.ToUInt64Value()] = round;
            return true;
        }

        public void AddOrUpdateTicketsInformation(Tickets tickets)
        {
            State.TicketsMap[tickets.PublicKey.ToStringValue()] = tickets;
        }

        public void SetTermSnapshot(TermSnapshot snapshot)
        {
            State.SnapshotMap[snapshot.TermNumber.ToUInt64Value()] = snapshot;
        }

        public void SetAlias(string publicKey, string alias)
        {
            State.AliasesMap[publicKey.ToStringValue()] = alias.ToStringValue();
            State.AliasesLookupMap[alias.ToStringValue()] = publicKey.ToStringValue();
        }

        public void SetMiningInterval(int miningInterval)
        {
            State.MiningIntervalField.Value = miningInterval;
        }

        public bool AddTermNumberToFirstRoundNumber(ulong termNumber, ulong firstRoundNumber)
        {
            var ri = State.TermToFirstRoundMap[termNumber.ToUInt64Value()];
            if (ri != null)
            {
                return false;
            }

            State.TermToFirstRoundMap[termNumber.ToUInt64Value()] = firstRoundNumber.ToUInt64Value();
            return true;
        }

        public bool SetMiners(Miners miners, bool gonnaReplaceSomeone = false)
        {
            // Miners for one specific term should only update once.
            var m = State.MinersMap[miners.TermNumber.ToUInt64Value()];
            if (gonnaReplaceSomeone || m == null)
            {
                State.MinersMap[miners.TermNumber.ToUInt64Value()] = miners;
                return true;
            }

            return false;
        }

        public bool SetSnapshot(TermSnapshot snapshot)
        {
            var s = State.SnapshotMap[snapshot.TermNumber.ToUInt64Value()];
            if (s != null)
            {
                return false;
            }

            State.SnapshotMap[snapshot.TermNumber.ToUInt64Value()] = snapshot;
            return true;
        }

        public bool IsMinerOfCurrentTerm(string publicKey)
        {
            if (TryToGetTermNumber(out var termNumber))
            {
                if (TryToGetMiners(termNumber, out var miners))
                {
                    return miners.PublicKeys.Contains(publicKey);
                }
            }

            return false;
        }

        #region Utilities

        private bool ValidateMinersList(Round round1, Round round2)
        {
            return true;

            // TODO:
            // If the miners are different, we need a further validation
            // to prove the missing (replaced) one should be kicked out.
        }

        private bool OutInValueAreNull(Round round)
        {
            return round.RealTimeMinersInformation.Values.Any(minerInRound =>
                minerInRound.OutValue != null || minerInRound.InValue != null);
        }

        private bool InValueIsNull(Round round)
        {
            return round.RealTimeMinersInformation.Values.All(m => m.InValue == null);
        }

        private bool ValidateVictories(Miners miners)
        {
            if (TryToGetVictories(out var victories))
            {
                return victories.GetMinersHash() == miners.GetMinersHash();
            }

            return false;
        }

        private bool RoundIdMatched(Round round)
        {
            if (TryToGetCurrentRoundInformation(out var currentRoundInStateDB))
            {
                return currentRoundInStateDB.RoundId == round.RoundId;
            }

            return false;
        }

        private bool NewOutValueFilled(Round round)
        {
            if (TryToGetCurrentRoundInformation(out var currentRoundInStateDB))
            {
                return currentRoundInStateDB.RealTimeMinersInformation.Values.Count(info => info.OutValue != null) + 1 ==
                       round.RealTimeMinersInformation.Values.Count(info => info.OutValue != null);
            }

            return false;
        }

        private bool AllOutValueFilled(string publicKey, out MinerInRound minerInformation)
        {
            minerInformation = null;
            if (TryToGetCurrentRoundInformation(out var currentRoundInStateDB))
            {
                if (currentRoundInStateDB.RealTimeMinersInformation.ContainsKey(publicKey))
                {
                    minerInformation = currentRoundInStateDB.RealTimeMinersInformation[publicKey];
                }

                return currentRoundInStateDB.RealTimeMinersInformation.Values.Count(info => info.OutValue != null) ==
                       GetProducerNumber();
            }

            return false;
        }

        private bool OwnOutValueFilled(string publicKey, out MinerInRound minerInformation)
        {
            minerInformation = null;
            if (TryToGetCurrentRoundInformation(out var currentRoundInStateDB))
            {
                if (currentRoundInStateDB.RealTimeMinersInformation.ContainsKey(publicKey))
                {
                    minerInformation = currentRoundInStateDB.RealTimeMinersInformation[publicKey];
                }

                return currentRoundInStateDB.RealTimeMinersInformation[publicKey].OutValue != null;
            }

            return false;
        }

        private bool TimeOverflow(Timestamp timestamp)
        {
            if (TryToGetCurrentRoundInformation(out var currentRoundInStateDB) &&
                TryToGetMiningInterval(out var miningInterval))
            {
                return currentRoundInStateDB.GetExtraBlockMiningTime(miningInterval).AddMilliseconds(-4000) <
                       timestamp.ToDateTime();
            }

            return false;
        }

        private Round GenerateFirstRoundOfNextTerm()
        {
            if (TryToGetTermNumber(out var termNumber) &&
                TryToGetRoundNumber(out var roundNumber) &&
                TryToGetVictories(out var victories) &&
                TryToGetMiningInterval(out var miningInterval))
            {
                return victories.GenerateFirstRoundOfNewTerm(miningInterval, roundNumber, termNumber);
            }

            return null;
        }

        private Round FillOutValueAndSignature(Hash outValue, Hash signature, string publicKey)
        {
            if (TryToGetCurrentRoundInformation(out var currentRoundInStateDB))
            {
                if (currentRoundInStateDB.RealTimeMinersInformation.ContainsKey(publicKey))
                {
                    currentRoundInStateDB.RealTimeMinersInformation[publicKey].OutValue = outValue;
                    currentRoundInStateDB.RealTimeMinersInformation[publicKey].Signature = signature;
                }

                return currentRoundInStateDB;
            }

            return new Round();
        }

        private DateTime GetExtraBlockMiningTime(int miningInterval)
        {
            if (TryToGetCurrentRoundInformation(out var currentRoundInStateDB))
            {
                return currentRoundInStateDB.GetExtraBlockMiningTime(miningInterval);
            }

            return DateTime.MaxValue;
        }

        private Transaction GenerateTransaction(string methodName, List<object> parameters)
        {
            var tx = new Transaction
            {
                From = Context.Sender,
                To = Context.Self,
                MethodName = methodName,
                Type = TransactionType.DposTransaction,
                Params = ByteString.CopyFrom(ParamsPacker.Pack(parameters.ToArray()))
            };

            return tx;
        }

        #endregion

        public ulong GetDividendsForEveryMiner(ulong minedBlocks)
        {
            return (ulong) (minedBlocks * DPoSContractConsts.ElfTokenPerBlock * DPoSContractConsts.MinersBasicRatio /
                            GetProducerNumber());
        }

        public ulong GetDividendsForTicketsCount(ulong minedBlocks)
        {
            return (ulong) (minedBlocks * DPoSContractConsts.ElfTokenPerBlock * DPoSContractConsts.MinersVotesRatio);
        }

        public ulong GetDividendsForReappointment(ulong minedBlocks)
        {
            return (ulong) (minedBlocks * DPoSContractConsts.ElfTokenPerBlock *
                            DPoSContractConsts.MinersReappointmentRatio);
        }

        public ulong GetDividendsForBackupNodes(ulong minedBlocks)
        {
            return (ulong) (minedBlocks * DPoSContractConsts.ElfTokenPerBlock * DPoSContractConsts.BackupNodesRatio);
        }

        public ulong GetDividendsForVoters(ulong minedBlocks)
        {
            return (ulong) (minedBlocks * DPoSContractConsts.ElfTokenPerBlock * DPoSContractConsts.VotersRatio);
        }

        public int GetProducerNumber()
        {
            return 17 + (DateTime.UtcNow.Year - 2019) * 2;
        }
    }
}