﻿using System;

namespace CTime2.Core.Extensions
{
    public static class DateTimeOffsetExtensions
    {
        public static DateTimeOffset StartOfMonth(this DateTimeOffset self)
        {
            return new DateTimeOffset(self.Year, self.Month, 1, 0, 0, 0, self.Offset);
        }

        public static DateTimeOffset EndOfMonth(this DateTimeOffset self)
        {
            return self.AddMonths(1).StartOfMonth().AddDays(-1);
        }
    }
}