using System;

namespace CTime2.Core.Services.Statistics
{
    public class CurrentBreak
    {
        public CurrentBreak(TimeSpan breakTime, DateTime preferredBreakTimeEnd)
        {
            this.BreakTime = breakTime;
            this.PreferredBreakTimeEnd = preferredBreakTimeEnd;
        }

        public TimeSpan BreakTime { get; }
        public DateTime PreferredBreakTimeEnd { get; }
    }
}