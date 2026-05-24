using System;
using System.Globalization;

namespace DateCountdown.Core
{
    public static class CountdownLogic
    {
        public static int CalculateDaysLeft(DateTimeOffset targetDate, DateTimeOffset now)
        {
            TimeSpan remaining = targetDate.ToUniversalTime() - now.ToUniversalTime();
            if (remaining > TimeSpan.Zero)
            {
                return (int)Math.Ceiling(remaining.TotalDays);
            }

            if (remaining < TimeSpan.Zero)
            {
                return -(int)Math.Ceiling(Math.Abs(remaining.TotalDays));
            }

            return 0;
        }

        public static int CalculateHoursLeft(DateTimeOffset targetDate, DateTimeOffset now)
        {
            TimeSpan remaining = targetDate.ToUniversalTime() - now.ToUniversalTime();
            return remaining > TimeSpan.Zero
                ? (int)Math.Ceiling(remaining.TotalHours)
                : 0;
        }

        public static bool IsExpired(DateTimeOffset targetDate, DateTimeOffset now)
        {
            return targetDate.ToUniversalTime() < now.ToUniversalTime();
        }

        public static DateTimeOffset ReadDateValue(object? value, DateTimeOffset fallback)
        {
            switch (value)
            {
                case DateTimeOffset date:
                    return date;
                case DateTime date:
                    return new DateTimeOffset(date);
                case string text when DateTimeOffset.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out DateTimeOffset parsedDate):
                    return parsedDate;
                default:
                    return fallback;
            }
        }
    }
}
