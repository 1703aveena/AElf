using System;
using System.Collections.Generic;
using System.Linq;
using AElf.Common;
using Google.Protobuf.WellKnownTypes;

// ReSharper disable once CheckNamespace
namespace AElf.Kernel
{
    // ReSharper disable InconsistentNaming
    public partial class Miners
    {
        public bool IsEmpty()
        {
            return !PublicKeys.Any();
        }

        public Term GenerateNewTerm(int miningInterval)
        {
            var dict = new Dictionary<string, int>();

            // First round
            foreach (var miner in PublicKeys)
            {
                dict.Add(miner, miner[0]);
            }

            var sortedMiningNodes =
                from obj in dict
                orderby obj.Value descending
                select obj.Key;

            var enumerable = sortedMiningNodes.ToList();

            var infosOfRound1 = new Round();

            var selected = PublicKeys.Count / 2;
            for (var i = 0; i < enumerable.Count; i++)
            {
                var minerInRound = new MinerInRound {IsExtraBlockProducer = false};

                if (i == selected)
                {
                    minerInRound.IsExtraBlockProducer = true;
                }

                minerInRound.Order = i + 1;
                minerInRound.Signature = Hash.Generate();
                minerInRound.ExpectedMiningTime =
                    GetTimestampOfUtcNow(i * miningInterval + GlobalConfig.AElfWaitFirstRoundTime);
                minerInRound.PublicKey = enumerable[i];

                infosOfRound1.RealTimeMinersInfo.Add(enumerable[i], minerInRound);
            }

            // Second round
            dict = new Dictionary<string, int>();

            foreach (var miner in PublicKeys)
            {
                dict.Add(miner, miner[0]);
            }

            sortedMiningNodes =
                from obj in dict
                orderby obj.Value descending
                select obj.Key;

            enumerable = sortedMiningNodes.ToList();

            var infosOfRound2 = new Round();

            var addition = enumerable.Count * miningInterval + miningInterval;

            selected = PublicKeys.Count / 2;
            for (var i = 0; i < enumerable.Count; i++)
            {
                var minerInRound = new MinerInRound {IsExtraBlockProducer = false};

                if (i == selected)
                {
                    minerInRound.IsExtraBlockProducer = true;
                }

                minerInRound.ExpectedMiningTime =
                    GetTimestampOfUtcNow(i * miningInterval + addition + GlobalConfig.AElfWaitFirstRoundTime);
                minerInRound.Order = i + 1;
                minerInRound.PublicKey = enumerable[i];

                infosOfRound2.RealTimeMinersInfo.Add(enumerable[i], minerInRound);
            }

            infosOfRound1.RoundNumber = 1;
            infosOfRound2.RoundNumber = 2;

            infosOfRound1.MiningInterval = miningInterval;
            infosOfRound2.MiningInterval = miningInterval;

            var term = new Term
            {
                FirstRound = infosOfRound1,
                SecondRound = infosOfRound2,
                Miners = new Miners
                {
                    TakeEffectRoundNumber = 2,
                    PublicKeys = {PublicKeys}
                },
                MiningInterval = miningInterval
            };

            return term;
        }

        public Round GenerateNextRound(Round previousRound)
        {
            var miningInterval = previousRound.MiningInterval;
            var round = new Round {RoundNumber = previousRound.RoundNumber + 1};

            // EBP will be the first miner of next round.
            var extraBlockProducer = previousRound.GetEBPInfo().PublicKey;

            var signatureDict = new Dictionary<Hash, string>();
            var orderDict = new Dictionary<int, string>();

            var blockProducerCount = previousRound.RealTimeMinersInfo.Count;

            foreach (var miner in previousRound.RealTimeMinersInfo.Values)
            {
                var s = miner.Signature;
                if (s == null)
                {
                    s = Hash.Generate();
                }

                signatureDict[s] = miner.PublicKey;
            }

            foreach (var sig in signatureDict.Keys)
            {
                var sigNum = BitConverter.ToUInt64(
                    BitConverter.IsLittleEndian ? sig.Value.Reverse().ToArray() : sig.Value.ToArray(), 0);
                var order = Math.Abs(GetModulus(sigNum, blockProducerCount));

                if (orderDict.ContainsKey(order))
                {
                    for (var i = 0; i < blockProducerCount; i++)
                    {
                        if (!orderDict.ContainsKey(i))
                        {
                            order = i;
                        }
                    }
                }

                orderDict.Add(order, signatureDict[sig]);
            }

            var extraBlockMiningTime = previousRound.GetEBPMiningTime().ToTimestamp();

            // Maybe because something happened with setting extra block time slot.
            if (extraBlockMiningTime.ToDateTime().AddMilliseconds(miningInterval * 1.5) <
                GetTimestampOfUtcNow().ToDateTime())
            {
                extraBlockMiningTime = GetTimestampOfUtcNow();
            }

            for (var i = 0; i < orderDict.Count; i++)
            {
                var minerInRound = new MinerInRound
                {
                    ExpectedMiningTime = GetTimestampWithOffset(extraBlockMiningTime,
                        i * miningInterval +
                        miningInterval * 2),
                    Order = i + 1
                };

                round.RealTimeMinersInfo[orderDict[i]] = minerInRound;
            }

            var newEBP = CalculateNextExtraBlockProducer(round);
            round.RealTimeMinersInfo[newEBP].IsExtraBlockProducer = true;

            // Exchange
            var oldOrder = round.RealTimeMinersInfo[extraBlockProducer].Order;
            round.RealTimeMinersInfo[extraBlockProducer].Order = 1;
            round.RealTimeMinersInfo.First().Value.Order = oldOrder;

            return round;
        }

        private string CalculateNextExtraBlockProducer(Round roundInfo)
        {
            var firstPlaceInfo = roundInfo.GetFirstPlaceMinerInfo();
            var sig = firstPlaceInfo.Signature;
            if (sig == null)
            {
                sig = Hash.Generate();
            }

            var sigNum = BitConverter.ToUInt64(
                BitConverter.IsLittleEndian ? sig.Value.Reverse().ToArray() : sig.Value.ToArray(), 0);
            var blockProducerCount = roundInfo.RealTimeMinersInfo.Count;
            var order = GetModulus(sigNum, blockProducerCount);

            var nextEBP = roundInfo.RealTimeMinersInfo.Keys.ToList()[order];

            return nextEBP;
        }

        /// <summary>
        /// Get local time
        /// </summary>
        /// <param name="offset">minutes</param>
        /// <returns></returns>
        private Timestamp GetTimestampOfUtcNow(int offset = 0)
        {
            return Timestamp.FromDateTime(DateTime.UtcNow.AddMilliseconds(offset));
        }

        private Timestamp GetTimestampWithOffset(Timestamp origin, int offset)
        {
            return Timestamp.FromDateTime(origin.ToDateTime().AddMilliseconds(offset));
        }

        /// <summary>
        /// In case of forgetting to check negative value.
        /// For now this method only used for generating order,
        /// so integer should be enough.
        /// </summary>
        /// <param name="uLongVal"></param>
        /// <param name="intVal"></param>
        /// <returns></returns>
        private int GetModulus(ulong uLongVal, int intVal)
        {
            return Math.Abs((int) (uLongVal % (ulong) intVal));
        }
    }
}