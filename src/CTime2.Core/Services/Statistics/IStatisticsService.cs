using System;
using System.Collections.Generic;
using CTime2.Core.Data;

namespace CTime2.Core.Services.Statistics
{
    public interface IStatisticsService
    {
        bool ShouldIncludeToday(List<TimesByDay> times);
        TimeSpan CalculateAverageWorkTime(List<TimesByDay> times, bool onlyWorkDays);
        TimeSpan CalculateTotalWorkTime(List<TimesByDay> times, bool onlyWorkDays);
        int CalculateWorkDayCount(List<TimesByDay> times, bool onlyWorkDays);
        TimeSpan CalculateAverageEnterTime(List<TimesByDay> times, bool onlyWorkDays);
        TimeSpan CalculateAverageLeaveTime(List<TimesByDay> times, bool onlyWorkDays);
        TimeSpan CalculateAverageBreakTime(List<TimesByDay> times, bool onlyWorkDays, bool onlyDaysWithBreak);
        TimeSpan CalculateOverTime(List<TimesByDay> times, bool onlyWorkDays);
        TodaysWorkEnd CalculateTodaysWorkEnd(TimesByDay timeToday, List<TimesByDay> times, bool onlyWorkDays);
        TimeSpan CalculateAverageOverTime(List<TimesByDay> times, bool onlyWorkDays);
        CurrentTime CalculateCurrentTime(Time currentTime);
    }
}