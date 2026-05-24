using System;
using System.Globalization;

namespace DateCountdown.Core
{
    public static class CountdownTimeZone
    {
        public const string AnywhereOnEarthId = "Dateline Standard Time";
        public const string ChinaId = "China Standard Time";
        public static readonly TimeSpan DefaultDeadlineTimeOfDay = new(23, 59, 0);

        public static string DefaultTimeZoneId => TimeZoneInfo.Local.Id;

        public static string NormalizeTimeZoneId(string? timeZoneId)
        {
            if (string.IsNullOrWhiteSpace(timeZoneId))
            {
                return DefaultTimeZoneId;
            }

            try
            {
                _ = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
                return timeZoneId;
            }
            catch (TimeZoneNotFoundException)
            {
                return DefaultTimeZoneId;
            }
            catch (InvalidTimeZoneException)
            {
                return DefaultTimeZoneId;
            }
        }

        public static TimeZoneInfo FindTimeZoneOrLocal(string? timeZoneId)
        {
            return TimeZoneInfo.FindSystemTimeZoneById(NormalizeTimeZoneId(timeZoneId));
        }

        public static DateTimeOffset CreateTargetDate(DateTime date, TimeSpan timeOfDay, string? timeZoneId)
        {
            TimeZoneInfo timeZone = FindTimeZoneOrLocal(timeZoneId);
            DateTime localDateTime = DateTime.SpecifyKind(date.Date + timeOfDay, DateTimeKind.Unspecified);
            while (timeZone.IsInvalidTime(localDateTime))
            {
                localDateTime = localDateTime.AddMinutes(1);
            }

            return new DateTimeOffset(localDateTime, timeZone.GetUtcOffset(localDateTime));
        }

        public static DateTimeOffset CreateDefaultTargetDate(DateTimeOffset now, string? timeZoneId = null)
        {
            string normalizedTimeZoneId = NormalizeTimeZoneId(timeZoneId);
            DateTime targetDate = GetDateInTimeZone(now, normalizedTimeZoneId);
            DateTimeOffset target = CreateTargetDate(targetDate, DefaultDeadlineTimeOfDay, normalizedTimeZoneId);
            return target >= now
                ? target
                : CreateTargetDate(targetDate.AddDays(1), DefaultDeadlineTimeOfDay, normalizedTimeZoneId);
        }

        public static DateTime GetDateInTimeZone(DateTimeOffset value, string? timeZoneId)
        {
            return TimeZoneInfo.ConvertTime(value, FindTimeZoneOrLocal(timeZoneId)).Date;
        }

        public static TimeSpan GetTimeOfDayInTimeZone(DateTimeOffset value, string? timeZoneId)
        {
            return TimeZoneInfo.ConvertTime(value, FindTimeZoneOrLocal(timeZoneId)).TimeOfDay;
        }

        public static string FormatTargetDateTime(DateTimeOffset targetDate, string? timeZoneId, CultureInfo? culture = null)
        {
            CultureInfo resolvedCulture = culture ?? CultureInfo.CurrentCulture;
            TimeZoneInfo timeZone = FindTimeZoneOrLocal(timeZoneId);
            DateTimeOffset localTarget = TimeZoneInfo.ConvertTime(targetDate, timeZone);
            return string.Format(
                resolvedCulture,
                "{0:g} ({1})",
                localTarget,
                FormatUtcOffset(localTarget.Offset));
        }

        public static string FormatUtcOffset(TimeSpan offset)
        {
            if (offset == TimeSpan.Zero)
            {
                return "UTC";
            }

            string sign = offset < TimeSpan.Zero ? "-" : "+";
            TimeSpan absoluteOffset = offset.Duration();
            return string.Format(
                CultureInfo.InvariantCulture,
                "UTC{0}{1:00}:{2:00}",
                sign,
                absoluteOffset.Hours,
                absoluteOffset.Minutes);
        }

        public static DateTimeOffset MigrateLegacyDateOnlyTarget(DateTimeOffset targetDate, string? timeZoneId)
        {
            if (targetDate.TimeOfDay != TimeSpan.Zero)
            {
                return targetDate;
            }

            return CreateTargetDate(targetDate.Date, DefaultDeadlineTimeOfDay, timeZoneId);
        }
    }
}
