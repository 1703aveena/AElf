using System;

// ReSharper disable once CheckNamespace
namespace AElf.Kernel
{
    public partial class VotingRecord
    {
        private TimeSpan PastTime => DateTime.UtcNow - VoteTimestamp.ToDateTime();
        
        public bool IsExpired()
        {
            uint lockDays = 0;
            foreach (var day in LockDaysList)
            {
                lockDays += day;
            }

            return PastTime.TotalDays >= lockDays;
        }

        public uint GetCurrentLockingDays()
        {
            uint lockDays = 0;
            foreach (var day in LockDaysList)
            {
                lockDays += day;
                if (lockDays > PastTime.TotalDays)
                {
                    return day;
                }
            }

            return 0;
        }
    }
}