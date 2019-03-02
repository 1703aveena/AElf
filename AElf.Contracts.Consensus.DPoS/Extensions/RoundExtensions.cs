using System;
using System.Linq;
using AElf.Common;
using AElf.Kernel;
using Google.Protobuf.WellKnownTypes;

namespace AElf.Contracts.Consensus.DPoS.Extensions
{
    public static class RoundExtensions
    {
        /// <summary>
        /// This method is only executable when the miners of this round is more than 1.
        /// </summary>
        /// <param name="round"></param>
        /// <returns></returns>
        public static int GetMiningInterval(this Round round)
        {
            if (round.RealTimeMinersInformation.Count < 2)
            {
                // TODO: Consider using assertion if mining interval is invalid.
                // 0 is supposed to be an invalid mining interval.
                return 0;
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
        public static Timestamp GetStartTime(this Round round)
        {
            return round.RealTimeMinersInformation.Values.First(m => m.Order == 1).ExpectedMiningTime;
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

            return round.GetStartTime().ToDateTime().AddMilliseconds(round.TotalMilliseconds(miningInterval))
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
        /// For now, if current time is behind the start of expected mining time slot,
        /// we can say this node missed his time slot.
        /// </summary>
        /// <param name="round"></param>
        /// <param name="publicKey"></param>
        /// <param name="timestamp"></param>
        /// <param name="minerInRound"></param>
        /// <returns></returns>
        public static bool IsTimeSlotPassed(this Round round, string publicKey, Timestamp timestamp,
            out MinerInRound minerInRound)
        {
            minerInRound = null;
            if (round.RealTimeMinersInformation.ContainsKey(publicKey))
            {
                minerInRound = round.RealTimeMinersInformation[publicKey];
                return minerInRound.ExpectedMiningTime.ToDateTime() < timestamp.ToDateTime();
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
        public static Timestamp ArrangeAbnormalMiningTime(this Round round, string publicKey, Timestamp timestamp,
            int miningInterval = 0)
        {
            if (miningInterval == 0)
            {
                miningInterval = round.GetMiningInterval();
            }

            if (!round.IsTimeSlotPassed(publicKey, timestamp, out var minerInRound))
            {
                return DateTime.MaxValue.ToUniversalTime().ToTimestamp();
            }

            if (round.RealTimeMinersInformation.ContainsKey(publicKey) && miningInterval > 0)
            {
                var distanceToRoundStartTime =
                    (timestamp.ToDateTime() - round.GetStartTime().ToDateTime()).TotalMilliseconds;
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

        public static MinerInRound GetFirstPlaceMinerInfo(this Round round)
        {
            return round.RealTimeMinersInformation.FirstOrDefault().Value;
        }

        public static Round Supplement(this Round round, Round previousRound)
        {
            foreach (var minerInRound in round.RealTimeMinersInformation.Values)
            {
                if (minerInRound.OutValue != null)
                {
                    continue;
                }

                minerInRound.MissedTimeSlots += 1;

                var inValue = Hash.Generate();
                var outValue = Hash.FromMessage(inValue);

                minerInRound.OutValue = outValue;
                minerInRound.InValue = inValue;

                var signature = previousRound.CalculateSignature(inValue);
                minerInRound.Signature = signature;
            }

            return round;
        }

        public static Round SupplementForFirstRound(this Round round)
        {
            foreach (var minerInRound in round.RealTimeMinersInformation.Values)
            {
                if (minerInRound.InValue != null && minerInRound.OutValue != null)
                {
                    continue;
                }

                minerInRound.MissedTimeSlots += 1;

                var inValue = Hash.Generate();
                var outValue = Hash.FromMessage(inValue);

                minerInRound.OutValue = outValue;
                minerInRound.InValue = inValue;
            }

            return round;
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
                round.RealTimeMinersInformation.Values.Aggregate(Hash.Default,
                    (current, minerInRound) => Hash.FromTwoHashes(current, minerInRound.Signature)));
        }

        public static Hash GetMinersHash(this Round round)
        {
            return Hash.FromMessage(round.RealTimeMinersInformation.Values.Select(m => m.PublicKey).OrderBy(p => p)
                .ToMiners());
        }

        public static ulong GetMinedBlocks(this Round round)
        {
            return round.RealTimeMinersInformation.Values.Select(mi => mi.ProducedBlocks)
                .Aggregate<ulong, ulong>(0, (current, @ulong) => current + @ulong);
        }

        public static bool CheckWhetherMostMinersMissedTimeSlots(this Round round)
        {
            if (Config.GetProducerNumber() == 1)
            {
                return false;
            }

            var missedMinersCount = 0;
            foreach (var minerInRound in round.RealTimeMinersInformation)
            {
                if (minerInRound.Value.LatestMissedTimeSlots == DPoSContractConsts.ForkDetectionRoundNumber)
                {
                    missedMinersCount++;
                }
            }

            return missedMinersCount >= (Config.GetProducerNumber() - 1) * DPoSContractConsts.ForkDetectionRoundNumber;
        }
    }
}