using System;

namespace CTime2.Core.Services.Statistics
{
    public class CurrentTime
    {
        public CurrentTime(TimeSpan workTime, TimeSpan? overTime, CurrentBreak currentBreak, bool isStillRunning)
        {
            this.WorkTime = workTime;
            this.OverTime = overTime;
            this.CurrentBreak = currentBreak;
            this.IsStillRunning = isStillRunning;
        }

        public TimeSpan WorkTime { get; }
        public TimeSpan? OverTime { get; }
        public CurrentBreak CurrentBreak { get; }
        public bool IsStillRunning { get; }
    }
}