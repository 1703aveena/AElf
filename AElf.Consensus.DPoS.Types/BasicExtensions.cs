using System;
using System.Collections.Generic;
using System.Linq;
using AElf.Common;
using AElf.Cryptography;
using AElf.Cryptography.SecretSharing;
using AElf.Kernel;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;

namespace AElf.Consensus.DPoS
{
    public static class BasicExtensions
    {
        public static ConsensusCommand GetConsensusCommand(this DPoSBehaviour behaviour, Round round,
            MinerInRound minerInRound, int miningInterval, DateTime dateTime)
        {
            switch (behaviour)
            {
                case DPoSBehaviour.UpdateValueWithoutPreviousInValue:
                    return new ConsensusCommand
                    {
                        NextBlockMiningLeftMilliseconds = minerInRound.Order * miningInterval,
                        LimitMillisecondsOfMiningBlock = miningInterval / minerInRound.PromisedTinyBlocks,
                        Hint = new DPoSHint
                        {
                            Behaviour = behaviour
                        }.ToByteString()
                    };
                
                case DPoSBehaviour.UpdateValue:
                    var expectedMiningTime = round.GetExpectedMiningTime(minerInRound.PublicKey);

                    return new ConsensusCommand
                    {
                        NextBlockMiningLeftMilliseconds = (int) (expectedMiningTime.ToDateTime() - dateTime)
                            .TotalMilliseconds,
                        LimitMillisecondsOfMiningBlock = miningInterval / minerInRound.PromisedTinyBlocks,
                        Hint = new DPoSHint
                        {
                            Behaviour = behaviour
                        }.ToByteString()
                    };
                case DPoSBehaviour.NextRound:
                    return new ConsensusCommand
                    {
                        NextBlockMiningLeftMilliseconds =
                            (int) (round.ArrangeAbnormalMiningTime(minerInRound.PublicKey, dateTime).ToDateTime() -
                                   dateTime).TotalMilliseconds,
                        LimitMillisecondsOfMiningBlock = miningInterval / minerInRound.PromisedTinyBlocks,
                        Hint = new DPoSHint
                        {
                            Behaviour = behaviour
                        }.ToByteString()
                    };
                case DPoSBehaviour.NextTerm:
                    return new ConsensusCommand
                    {
                        NextBlockMiningLeftMilliseconds =
                            (int) (round.ArrangeAbnormalMiningTime(minerInRound.PublicKey, dateTime).ToDateTime() -
                                   dateTime).TotalMilliseconds,
                        LimitMillisecondsOfMiningBlock = miningInterval / minerInRound.PromisedTinyBlocks,
                        Hint = new DPoSHint
                        {
                            Behaviour = behaviour
                        }.ToByteString()
                    };
                case DPoSBehaviour.Invalid:
                    return new ConsensusCommand
                    {
                        NextBlockMiningLeftMilliseconds = int.MaxValue,
                        LimitMillisecondsOfMiningBlock = int.MaxValue,
                        Hint = new DPoSHint
                        {
                            Behaviour = behaviour
                        }.ToByteString()
                    };
                default:
                    return new ConsensusCommand
                    {
                        NextBlockMiningLeftMilliseconds = int.MaxValue,
                        LimitMillisecondsOfMiningBlock = int.MaxValue,
                        Hint = new DPoSHint
                        {
                            Behaviour = DPoSBehaviour.Invalid
                        }.ToByteString()
                    };
            }
        }

        /// <summary>
        /// This method is only executable when the miners of this round is more than 1.
        /// </summary>
        /// <param name="round"></param>
        /// <returns></returns>
        public static int GetMiningInterval(this Round round)
        {
            if (round.RealTimeMinersInformation.Count < 2)
            {
                // Just appoint the mining interval for single miner.
                return 4000;
            }

            var firstTwoMiners = round.RealTimeMinersInformation.Values.Where(m => m.Order == 1 || m.Order == 2)
                .ToList();
            var distance =
                (int) (firstTwoMiners[1].ExpectedMiningTime.ToDateTime() -
                       firstTwoMiners[0].ExpectedMiningTime.ToDateTime())
                .TotalMilliseconds;
            return distance > 0 ? distance : -distance;
        }

        /// <summary>
        /// In current DPoS design, each miner produce his block in one time slot, then the extra block producer
        /// produce a block to terminate current round and confirm the mining order of next round.
        /// So totally, the time of one round is:
        /// MiningInterval * MinersCount + MiningInterval.
        /// </summary>
        /// <param name="round"></param>
        /// <param name="miningInterval"></param>
        /// <returns></returns>                                                
        public static int TotalMilliseconds(this Round round, int miningInterval = 0)
        {
            if (miningInterval == 0)
            {
                miningInterval = round.GetMiningInterval();
            }

            return round.RealTimeMinersInformation.Count * miningInterval + miningInterval;
        }

        /// <summary>
        /// Actually the expected mining time of the miner whose order is 1.
        /// </summary>
        /// <param name="round"></param>
        /// <returns></returns>
        public static DateTime GetStartTime(this Round round)
        {
            return round.RealTimeMinersInformation.Values.First(m => m.Order == 1).ExpectedMiningTime.ToDateTime();
        }

        /// <summary>
        /// This method for now is able to handle the situation of a miner keeping offline so many rounds,
        /// by using missedRoundsCount.
        /// </summary>
        /// <param name="round"></param>
        /// <param name="miningInterval"></param>
        /// <param name="missedRoundsCount"></param>
        /// <returns></returns>
        public static Timestamp GetExpectedEndTime(this Round round, int missedRoundsCount = 0, int miningInterval = 0)
        {
            if (miningInterval == 0)
            {
                miningInterval = round.GetMiningInterval();
            }

            return round.GetStartTime().AddMilliseconds(round.TotalMilliseconds(miningInterval))
                // Arrange an ending time if this node missed so many rounds.
                .AddMilliseconds(missedRoundsCount * round.TotalMilliseconds(miningInterval))
                .ToTimestamp();
        }

        /// <summary>
        /// Simply read the expected mining time of provided public key from round information.
        /// Do not check this node missed his time slot or not.
        /// </summary>
        /// <param name="round"></param>
        /// <param name="publicKey"></param>
        /// <returns></returns>
        public static Timestamp GetExpectedMiningTime(this Round round, string publicKey)
        {
            if (round.RealTimeMinersInformation.ContainsKey(publicKey))
            {
                return round.RealTimeMinersInformation[publicKey].ExpectedMiningTime;
            }

            return DateTime.MaxValue.ToUniversalTime().ToTimestamp();
        }

        /// <summary>
        /// For now, if current time is behind the end of expected mining time slot,
        /// we can say this node missed his time slot.
        /// </summary>
        /// <param name="round"></param>
        /// <param name="publicKey"></param>
        /// <param name="dateTime"></param>
        /// <param name="minerInRound"></param>
        /// <returns></returns>
        public static bool IsTimeSlotPassed(this Round round, string publicKey, DateTime dateTime,
            out MinerInRound minerInRound)
        {
            minerInRound = null;
            var miningInterval = round.GetMiningInterval();
            if (round.RealTimeMinersInformation.ContainsKey(publicKey))
            {
                minerInRound = round.RealTimeMinersInformation[publicKey];
                return minerInRound.ExpectedMiningTime.ToDateTime().AddMilliseconds((double) miningInterval / 2) <
                       dateTime;
            }

            return false;
        }

        /// <summary>
        /// If one node produced block this round or missed his time slot,
        /// whatever how long he missed, we can give him a consensus command with new time slot
        /// to produce a block (for terminating current round and start new round).
        /// The schedule generated by this command will be cancelled
        /// if this node executed blocks from other nodes.
        /// 
        /// Notice:
        /// This method shouldn't return the expected mining time from round information.
        /// To prevent this kind of misuse, this method will return a invalid timestamp
        /// when this node hasn't missed his time slot.
        /// </summary>
        /// <returns></returns>
        public static Timestamp ArrangeAbnormalMiningTime(this Round round, string publicKey, DateTime dateTime,
            int miningInterval = 0)
        {
            if (miningInterval == 0)
            {
                miningInterval = round.GetMiningInterval();
            }

            if (round.RoundNumber == 1)
            {
                return dateTime.AddMilliseconds(round.RealTimeMinersInformation.Count * miningInterval)
                    .ToTimestamp();
            }

            if (!round.IsTimeSlotPassed(publicKey, dateTime, out var minerInRound) && minerInRound.OutValue == null)
            {
                return DateTime.MaxValue.ToUniversalTime().ToTimestamp();
            }

            if (round.GetExtraBlockProducerInformation().PublicKey == publicKey)
            {
                var distance = (round.GetExtraBlockMiningTime() - dateTime).TotalMilliseconds;
                if (distance > 0)
                {
                    return dateTime.AddMilliseconds(distance).ToTimestamp();
                }
            }

            if (round.RealTimeMinersInformation.ContainsKey(publicKey) && miningInterval > 0)
            {
                var distanceToRoundStartTime =
                    (dateTime - round.GetStartTime()).TotalMilliseconds;
                var missedRoundsCount = (int) (distanceToRoundStartTime / round.TotalMilliseconds(miningInterval));
                var expectedEndTime = round.GetExpectedEndTime(missedRoundsCount, miningInterval);
                return expectedEndTime.ToDateTime().AddMilliseconds(minerInRound.Order * miningInterval).ToTimestamp();
            }

            // Never do the mining if this node has no privilege to mime or the mining interval is invalid.
            return DateTime.MaxValue.ToUniversalTime().ToTimestamp();
        }

        public static MinerInRound GetExtraBlockProducerInformation(this Round round)
        {
            return round.RealTimeMinersInformation.First(bp => bp.Value.IsExtraBlockProducer).Value;
        }

        public static DateTime GetExtraBlockMiningTime(this Round round, int miningInterval = 0)
        {
            if (miningInterval == 0)
            {
                miningInterval = round.GetMiningInterval();
            }

            return round.RealTimeMinersInformation.OrderBy(m => m.Value.ExpectedMiningTime.ToDateTime()).Last().Value
                .ExpectedMiningTime.ToDateTime()
                .AddMilliseconds(miningInterval);
        }

        /// <summary>
        /// Maybe tune other miners' supposed order of next round,
        /// will record this purpose to their FinalOrderOfNextRound field.
        /// </summary>
        /// <param name="round"></param>
        /// <param name="publicKey"></param>
        /// <returns></returns>
        public static ToUpdate GenerateInformationToUpdateConsensus(this Round round, string publicKey)
        {
            if (!round.RealTimeMinersInformation.ContainsKey(publicKey))
            {
                return null;
            }

            var tuneOrderInformation = round.RealTimeMinersInformation.Values
                .Where(m => m.FinalOrderOfNextRound != m.SupposedOrderOfNextRound)
                .ToDictionary(m => m.PublicKey, m => m.FinalOrderOfNextRound);

            var minerInRound = round.RealTimeMinersInformation[publicKey];
            return new ToUpdate
            {
                OutValue = minerInRound.OutValue,
                Signature = minerInRound.Signature,
                PreviousInValue = minerInRound.PreviousInValue ?? Hash.Empty,
                RoundId = round.RoundId,
                PromiseTinyBlocks = minerInRound.PromisedTinyBlocks,
                ActualMiningTime = minerInRound.ActualMiningTime,
                SupposedOrderOfNextRound = minerInRound.SupposedOrderOfNextRound,
                TuneOrderInformation = {tuneOrderInformation},
                EncryptedInValues = {minerInRound.EncryptedInValues},
                DecryptedPreviousInValues = {minerInRound.DecryptedPreviousInValues}
            };
        }

        public static Round ApplyNormalConsensusData(this Round round, string publicKey, Hash outValue, Hash signature,
            DateTime dateTime)
        {
            if (!round.RealTimeMinersInformation.ContainsKey(publicKey))
            {
                return round;
            }

            round.RealTimeMinersInformation[publicKey].ActualMiningTime = dateTime.ToTimestamp();
            round.RealTimeMinersInformation[publicKey].OutValue = outValue;
            round.RealTimeMinersInformation[publicKey].Signature = signature;

            var minersCount = round.RealTimeMinersInformation.Count;
            var sigNum =
                BitConverter.ToInt64(
                    BitConverter.IsLittleEndian ? signature.Value.Reverse().ToArray() : signature.Value.ToArray(),
                    0);
            var supposedOrderOfNextRound = GetAbsModulus(sigNum, minersCount) + 1;

            // Check the existence of conflicts about OrderOfNextRound.
            // If so, modify others'.
            var conflicts = round.RealTimeMinersInformation.Values
                .Where(i => i.FinalOrderOfNextRound == supposedOrderOfNextRound).ToList();

            foreach (var orderConflictedMiner in conflicts)
            {
                Console.WriteLine("Detected conflict miner: " + orderConflictedMiner.PublicKey);
                // Though multiple conflicts should be wrong, we can still arrange their orders of next round.

                for (var i = supposedOrderOfNextRound + 1; i < minersCount * 2; i++)
                {
                    var maybeNewOrder = i > minersCount ? i % minersCount : i;
                    if (round.RealTimeMinersInformation.Values.All(m => m.FinalOrderOfNextRound != maybeNewOrder))
                    {
                        Console.WriteLine($"Try to tune order from {round.RealTimeMinersInformation[orderConflictedMiner.PublicKey].FinalOrderOfNextRound} to {maybeNewOrder}");
                        round.RealTimeMinersInformation[orderConflictedMiner.PublicKey].FinalOrderOfNextRound =
                            maybeNewOrder;
                        break;
                    }
                }
            }

            round.RealTimeMinersInformation[publicKey].SupposedOrderOfNextRound = supposedOrderOfNextRound;
            // Initialize FinalOrderOfNextRound as the value of SupposedOrderOfNextRound
            round.RealTimeMinersInformation[publicKey].FinalOrderOfNextRound = supposedOrderOfNextRound;

            return round;
        }

        public static bool GenerateNextRoundInformation(this Round round, DateTime dateTime,
            Timestamp blockchainStartTimestamp, out Round nextRound)
        {
            nextRound = new Round();

            // Check: If one miner's OrderOfNextRound isn't 0, his must published his signature.
            var minersMinedCurrentRound =
                round.RealTimeMinersInformation.Values.Where(m => m.SupposedOrderOfNextRound != 0).ToList();
            if (minersMinedCurrentRound.Any(m => m.Signature == null))
            {
                return false;
            }

            // TODO: Check: No order conflicts for next round.

            var miningInterval = round.GetMiningInterval();
            nextRound.RoundNumber = round.RoundNumber + 1;
            nextRound.TermNumber = round.TermNumber;
            nextRound.BlockchainAge =
                (long) (dateTime - blockchainStartTimestamp.ToDateTime())
                .TotalMinutes; // TODO: Change to TotalDays after testing.

            // Set next round miners' information of miners who successfully mined during this round.
            foreach (var minerInRound in minersMinedCurrentRound.OrderBy(m => m.FinalOrderOfNextRound))
            {
                var order = minerInRound.FinalOrderOfNextRound;
                nextRound.RealTimeMinersInformation[minerInRound.PublicKey] = new MinerInRound
                {
                    PublicKey = minerInRound.PublicKey,
                    Order = order,
                    ExpectedMiningTime = dateTime.ToTimestamp().GetArrangedTimestamp(order, miningInterval),
                    PromisedTinyBlocks = minerInRound.PromisedTinyBlocks
                };
            }

            // Set miners' information of miners missed their time slot in current round.
            var minersNotMinedCurrentRound =
                round.RealTimeMinersInformation.Values.Where(m => m.SupposedOrderOfNextRound == 0).ToList();
            var minersCount = round.RealTimeMinersInformation.Count;
            var occupiedOrders = round.RealTimeMinersInformation.Values.Select(m => m.FinalOrderOfNextRound).ToList();
            var ableOrders = Enumerable.Range(1, minersCount).Where(i => !occupiedOrders.Contains(i)).ToList();
            for (var i = 0; i < minersNotMinedCurrentRound.Count; i++)
            {
                var order = ableOrders[i];
                var minerInRound = minersNotMinedCurrentRound[i];
                nextRound.RealTimeMinersInformation[minerInRound.PublicKey] = new MinerInRound
                {
                    PublicKey = minersNotMinedCurrentRound[i].PublicKey,
                    Order = order,
                    ExpectedMiningTime = dateTime.ToTimestamp().GetArrangedTimestamp(order, miningInterval),
                    PromisedTinyBlocks = minerInRound.PromisedTinyBlocks,
                };
            }

            // Calculate extra block producer order and set the producer.
            var extraBlockProducerOrder = round.CalculateNextExtraBlockProducerOrder();
            var expectedExtraBlockProducer =
                nextRound.RealTimeMinersInformation.Values.FirstOrDefault(m => m.Order == extraBlockProducerOrder);
            if (expectedExtraBlockProducer == null)
            {
                nextRound.RealTimeMinersInformation.Values.First().IsExtraBlockProducer = true;
            }
            else
            {
                expectedExtraBlockProducer.IsExtraBlockProducer = true;
            }

            return true;
        }

        private static Timestamp GetArrangedTimestamp(this Timestamp timestamp, int order, int miningInterval)
        {
            return timestamp.ToDateTime().AddMilliseconds(miningInterval * order).ToTimestamp();
        }

        private static int CalculateNextExtraBlockProducerOrder(this Round round)
        {
            var firstPlaceInfo = round.GetFirstPlaceMinerInformation();
            if (firstPlaceInfo == null)
            {
                // If no miner produce block during this round, just appoint the first miner to be the extra block producer of next round.
                return 1;
            }

            var signature = firstPlaceInfo.Signature;
            var sigNum = BitConverter.ToInt64(
                BitConverter.IsLittleEndian ? signature.Value.Reverse().ToArray() : signature.Value.ToArray(), 0);
            var blockProducerCount = round.RealTimeMinersInformation.Count;
            var order = GetAbsModulus(sigNum, blockProducerCount) + 1;
            return order;
        }

        /// <summary>
        /// Get the first valid (mined) miner's information, which means this miner's signature shouldn't be empty.
        /// </summary>
        /// <param name="round"></param>
        /// <returns></returns>
        public static MinerInRound GetFirstPlaceMinerInformation(this Round round)
        {
            return round.RealTimeMinersInformation.Values.OrderBy(m => m.Order)
                .FirstOrDefault(m => m.Signature != null);
        }

        public static Hash CalculateSignature(this Round round, Hash inValue)
        {
            // Check the signatures
            foreach (var minerInRound in round.RealTimeMinersInformation)
            {
                if (minerInRound.Value.Signature == null)
                {
                    minerInRound.Value.Signature = Hash.FromString(minerInRound.Key);
                }
            }

            return Hash.FromTwoHashes(inValue,
                round.RealTimeMinersInformation.Values.Aggregate(Hash.Empty,
                    (current, minerInRound) => Hash.FromTwoHashes(current, minerInRound.Signature)));
        }

        public static Int64Value ToInt64Value(this long value)
        {
            return new Int64Value {Value = value};
        }

        public static StringValue ToStringValue(this string value)
        {
            return new StringValue {Value = value};
        }

        /// <summary>
        /// Include both min and max value.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <returns></returns>
        public static bool InRange(this int value, int min, int max)
        {
            return value >= min && value <= max;
        }

        public static Round GenerateFirstRoundOfNewTerm(this Miners miners, int miningInterval,
            DateTime currentBlockTime, long currentRoundNumber = 0, long currentTermNumber = 0)
        {
            var dict = new Dictionary<string, int>();

            foreach (var miner in miners.PublicKeys)
            {
                dict.Add(miner, miner[0]);
            }

            var sortedMiners =
                (from obj in dict
                    orderby obj.Value descending
                    select obj.Key).ToList();

            var round = new Round();

            for (var i = 0; i < sortedMiners.Count; i++)
            {
                var minerInRound = new MinerInRound();

                // The first miner will be the extra block producer of first round of each term.
                if (i == 0)
                {
                    minerInRound.IsExtraBlockProducer = true;
                }

                minerInRound.PublicKey = sortedMiners[i];
                minerInRound.Order = i + 1;
                // Signatures totally randomized.
                //minerInRound.Signature = Hash.Generate();
                minerInRound.ExpectedMiningTime =
                    currentBlockTime.AddMilliseconds((i * miningInterval) + miningInterval).ToTimestamp();
                minerInRound.PromisedTinyBlocks = 1;
                // Should be careful during validation.
                minerInRound.PreviousInValue = Hash.Empty;

                round.RealTimeMinersInformation.Add(sortedMiners[i], minerInRound);
            }

            round.RoundNumber = currentRoundNumber + 1;
            round.TermNumber = currentTermNumber + 1;

            return round;
        }

        public static Hash GetMinersHash(this Miners miners)
        {
            var orderedMiners = miners.PublicKeys.OrderBy(p => p);
            return Hash.FromString(orderedMiners.Aggregate("", (current, publicKey) => current + publicKey));
        }

        public static bool IsTimeToChangeTerm(this Round round, Round previousRound, DateTime blockchainStartTime,
            long termNumber)
        {
            var minersCount = previousRound.RealTimeMinersInformation.Values.Count(m => m.OutValue != null);
            var minimumCount = ((int) ((minersCount * 2d) / 3)) + 1;
            var approvalsCount = round.RealTimeMinersInformation.Values.Where(m => m.ActualMiningTime != null)
                .Select(m => m.ActualMiningTime)
                .Count(t => IsTimeToChangeTerm(blockchainStartTime, t.ToDateTime(), termNumber));
            return approvalsCount >= minimumCount;
        }

        /// <summary>
        /// If DaysEachTerm == 7:
        /// 1, 1, 1 => 0 != 1 - 1 => false
        /// 1, 2, 1 => 0 != 1 - 1 => false
        /// 1, 8, 1 => 1 != 1 - 1 => true => term number will be 2
        /// 1, 9, 2 => 1 != 2 - 1 => false
        /// 1, 15, 2 => 2 != 2 - 1 => true => term number will be 3.
        /// </summary>
        /// <param name="blockchainStartTimestamp"></param>
        /// <param name="termNumber"></param>
        /// <param name="blockProducedTimestamp"></param>
        /// <returns></returns>
        private static bool IsTimeToChangeTerm(DateTime blockchainStartTimestamp, DateTime blockProducedTimestamp,
            long termNumber)
        {
            return (long) (blockProducedTimestamp - blockchainStartTimestamp).TotalMinutes /
                   ConsensusDPoSConsts.DaysEachTerm != termNumber - 1;
        }

        public static long GetMinedBlocks(this Round round)
        {
            return round.RealTimeMinersInformation.Values.Sum(minerInRound => minerInRound.ProducedBlocks);
        }

        public static void AddCandidate(this Candidates candidates, byte[] publicKey)
        {
            candidates.PublicKeys.Add(publicKey.ToHex());
            candidates.Addresses.Add(Address.FromPublicKey(publicKey));
        }

        public static bool RemoveCandidate(this Candidates candidates, byte[] publicKey)
        {
            var result1 = candidates.PublicKeys.Remove(publicKey.ToHex());
            var result2 = candidates.Addresses.Remove(Address.FromPublicKey(publicKey));
            return result1 && result2;
        }

        public static bool IsExpired(this VotingRecord votingRecord, long currentAge)
        {
            var lockExpiredAge = votingRecord.VoteAge;
            foreach (var day in votingRecord.LockDaysList)
            {
                lockExpiredAge += day;
            }

            return lockExpiredAge <= currentAge;
        }

        public static Miners ToMiners(this List<string> minerPublicKeys, long termNumber = 0)
        {
            return new Miners
            {
                PublicKeys = {minerPublicKeys},
                Addresses = {minerPublicKeys.Select(p => Address.FromPublicKey(ByteArrayHelpers.FromHexString(p)))},
                TermNumber = termNumber
            };
        }

        // TODO: Add test cases.
        /// <summary>
        /// Check the equality of time slots of miners.
        /// Also, the mining interval shouldn't be 0.
        /// </summary>
        /// <param name="round"></param>
        /// <returns></returns>
        public static ValidationResult CheckTimeSlots(this Round round)
        {
            var miners = round.RealTimeMinersInformation.Values.OrderBy(m => m.Order).ToList();
            if (miners.Count == 1)
            {
                // No need to check single node.
                return new ValidationResult {Success = true};
            }

            if (miners.Any(m => m.ExpectedMiningTime == null))
            {
                return new ValidationResult {Success = false, Message = "Incorrect expected mining time."};
            }

            var baseMiningInterval =
                (miners[1].ExpectedMiningTime.ToDateTime() - miners[0].ExpectedMiningTime.ToDateTime())
                .TotalMilliseconds;

            if (baseMiningInterval <= 0)
            {
                return new ValidationResult {Success = false, Message = $"Mining interval must greater than 0.\n{round}"};
            }

            for (var i = 1; i < miners.Count - 1; i++)
            {
                var miningInterval =
                    (miners[i + 1].ExpectedMiningTime.ToDateTime() - miners[i].ExpectedMiningTime.ToDateTime())
                    .TotalMilliseconds;
                if (Math.Abs(miningInterval - baseMiningInterval) > baseMiningInterval)
                {
                    return new ValidationResult {Success = false, Message = "Time slots are so different."};
                }
            }

            return new ValidationResult {Success = true};
        }

        private static int GetAbsModulus(long longValue, int intValue)
        {
            return Math.Abs((int) longValue % intValue);
        }
    }
}