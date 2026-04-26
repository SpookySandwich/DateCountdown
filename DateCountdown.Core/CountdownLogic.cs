using System;
using System.Globalization;

namespace DateCountdown.Core
{
    public static class CountdownLogic
    {
        public static int CalculateDaysLeft(DateTimeOffset targetDate, DateTimeOffset now)
        {
            return (int)(targetDate.Date - now.Date).TotalDays;
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
