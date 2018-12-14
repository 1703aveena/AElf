using System.Collections.Generic;
using System.Linq;
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;

// ReSharper disable once CheckNamespace
namespace AElf.Kernel
{
    public static class Extensions
    {
        public static UInt64Value ToUInt64Value(this ulong value)
        {
            return new UInt64Value {Value = value};
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

        public static Candidates ToCandidates(this IEnumerable<string> candidatesList)
        {
            return new Candidates {PublicKeys = {candidatesList}};
        }

        public static Miners ToMiners(this IEnumerable<string> minerPublicKeys)
        {
            return new Miners {PublicKeys = {minerPublicKeys}};
        }

        public static string ToAString(this IEnumerable<string> minerPublicKeys)
        {
            var res = minerPublicKeys.Aggregate("", (current, minerPublicKey) => current + minerPublicKey + ";");
            return res.Substring(0, res.Length - 1);
        }

        /// <summary>
        /// For calculating hash.
        /// </summary>
        /// <param name="votingRecord"></param>
        /// <returns></returns>
        public static VotingRecord ToSimpleRecord(this VotingRecord votingRecord)
        {
            return new VotingRecord
            {
                Count = votingRecord.Count,
                From = votingRecord.From,
                To = votingRecord.To,
                VoteTimestamp = votingRecord.VoteTimestamp
            };
        }
    }
}