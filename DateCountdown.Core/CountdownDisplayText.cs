using System;
using System.Globalization;

namespace DateCountdown.Core
{
    public sealed class CountdownDisplayText
    {
        private const string FallbackDefaultTitle = "Date Countdown";
        private const string FallbackOneDayLeftFormat = "{0} day left";
        private const string FallbackManyDaysLeftFormat = "{0} days left";
        private const string FallbackDueTodayText = "Due today";
        private const string FallbackOneHourLeftFormat = "{0} hour left";
        private const string FallbackManyHoursLeftFormat = "{0} hours left";
        private const string FallbackOneDayOverdueFormat = "{0} day overdue";
        private const string FallbackManyDaysOverdueFormat = "{0} days overdue";
        private const string FallbackOneDayOneHourLeftFormat = "{0} day {1} hour left";
        private const string FallbackOneDayManyHoursLeftFormat = "{0} day {1} hours left";
        private const string FallbackManyDaysOneHourLeftFormat = "{0} days {1} hour left";
        private const string FallbackManyDaysManyHoursLeftFormat = "{0} days {1} hours left";
        private const string FallbackOneDayDurationFormat = "{0} day";
        private const string FallbackManyDaysDurationFormat = "{0} days";
        private const string FallbackOneHourDurationFormat = "{0} hour";
        private const string FallbackManyHoursDurationFormat = "{0} hours";

        public CountdownDisplayText(CountdownDisplayTextResources resources)
        {
            DefaultTitle = UseFallback(resources.DefaultTitle, FallbackDefaultTitle);
            OneDayLeftFormat = UseFallback(resources.OneDayLeftFormat, FallbackOneDayLeftFormat);
            ManyDaysLeftFormat = UseFallback(resources.ManyDaysLeftFormat, FallbackManyDaysLeftFormat);
            DueTodayText = UseFallback(resources.DueTodayText, FallbackDueTodayText);
            OneHourLeftFormat = UseFallback(resources.OneHourLeftFormat, FallbackOneHourLeftFormat);
            ManyHoursLeftFormat = UseFallback(resources.ManyHoursLeftFormat, FallbackManyHoursLeftFormat);
            OneDayOverdueFormat = UseFallback(resources.OneDayOverdueFormat, FallbackOneDayOverdueFormat);
            ManyDaysOverdueFormat = UseFallback(resources.ManyDaysOverdueFormat, FallbackManyDaysOverdueFormat);
            OneDayOneHourLeftFormat = UseFallback(resources.OneDayOneHourLeftFormat, FallbackOneDayOneHourLeftFormat);
            OneDayManyHoursLeftFormat = UseFallback(resources.OneDayManyHoursLeftFormat, FallbackOneDayManyHoursLeftFormat);
            ManyDaysOneHourLeftFormat = UseFallback(resources.ManyDaysOneHourLeftFormat, FallbackManyDaysOneHourLeftFormat);
            ManyDaysManyHoursLeftFormat = UseFallback(resources.ManyDaysManyHoursLeftFormat, FallbackManyDaysManyHoursLeftFormat);
            OneDayDurationFormat = UseFallback(resources.OneDayDurationFormat, FallbackOneDayDurationFormat);
            ManyDaysDurationFormat = UseFallback(resources.ManyDaysDurationFormat, FallbackManyDaysDurationFormat);
            OneHourDurationFormat = UseFallback(resources.OneHourDurationFormat, FallbackOneHourDurationFormat);
            ManyHoursDurationFormat = UseFallback(resources.ManyHoursDurationFormat, FallbackManyHoursDurationFormat);
        }

        public static CountdownDisplayText FromResources(Func<string, string?> getString)
        {
            return new CountdownDisplayText(new CountdownDisplayTextResources
            {
                DefaultTitle = getString("AppName"),
                OneDayLeftFormat = getString("DaysLeftOneText"),
                ManyDaysLeftFormat = getString("DaysLeftManyText"),
                DueTodayText = getString("DueTodayText"),
                OneHourLeftFormat = getString("HoursLeftOneText"),
                ManyHoursLeftFormat = getString("HoursLeftManyText"),
                OneDayOverdueFormat = getString("DaysOverdueOneText"),
                ManyDaysOverdueFormat = getString("DaysOverdueManyText"),
                OneDayOneHourLeftFormat = getString("DaysHoursLeftOneDayOneHourText"),
                OneDayManyHoursLeftFormat = getString("DaysHoursLeftOneDayManyHoursText"),
                ManyDaysOneHourLeftFormat = getString("DaysHoursLeftManyDaysOneHourText"),
                ManyDaysManyHoursLeftFormat = getString("DaysHoursLeftManyDaysManyHoursText"),
                OneDayDurationFormat = getString("DurationOneDayText"),
                ManyDaysDurationFormat = getString("DurationManyDaysText"),
                OneHourDurationFormat = getString("DurationOneHourText"),
                ManyHoursDurationFormat = getString("DurationManyHoursText")
            });
        }

        public string DefaultTitle { get; }

        public string OneDayLeftFormat { get; }

        public string ManyDaysLeftFormat { get; }

        public string DueTodayText { get; }

        public string OneHourLeftFormat { get; }

        public string ManyHoursLeftFormat { get; }

        public string OneDayOverdueFormat { get; }

        public string ManyDaysOverdueFormat { get; }

        public string OneDayOneHourLeftFormat { get; }

        public string OneDayManyHoursLeftFormat { get; }

        public string ManyDaysOneHourLeftFormat { get; }

        public string ManyDaysManyHoursLeftFormat { get; }

        public string OneDayDurationFormat { get; }

        public string ManyDaysDurationFormat { get; }

        public string OneHourDurationFormat { get; }

        public string ManyHoursDurationFormat { get; }

        public string FormatTitle(string? title)
        {
            string normalizedTitle = CountdownItem.NormalizeTitle(title);
            return string.IsNullOrWhiteSpace(normalizedTitle) ? DefaultTitle : normalizedTitle;
        }

        public string FormatDaysLeft(int daysLeft, CultureInfo? culture = null)
        {
            if (daysLeft < 0)
            {
                int overdueDays = Math.Abs(daysLeft);
                return FormatNumberedText(
                    overdueDays,
                    overdueDays == 1 ? OneDayOverdueFormat : ManyDaysOverdueFormat,
                    overdueDays == 1 ? FallbackOneDayOverdueFormat : FallbackManyDaysOverdueFormat,
                    culture);
            }

            if (daysLeft == 0)
            {
                return DueTodayText;
            }

            return FormatNumberedText(
                daysLeft,
                daysLeft == 1 ? OneDayLeftFormat : ManyDaysLeftFormat,
                daysLeft == 1 ? FallbackOneDayLeftFormat : FallbackManyDaysLeftFormat,
                culture);
        }

        public string FormatCountdown(
            DateTimeOffset targetDate,
            DateTimeOffset now,
            CountdownPreferences preferences,
            CultureInfo? culture = null)
        {
            int daysLeft = CountdownLogic.CalculateDaysLeft(targetDate, now);
            if (daysLeft < 0)
            {
                return FormatDaysLeft(daysLeft, culture);
            }

            return preferences.HourDisplayMode switch
            {
                CountdownHourDisplayMode.HoursOnly => FormatHoursLeft(CountdownLogic.CalculateHoursLeft(targetDate, now), culture),
                CountdownHourDisplayMode.DaysAndHours => FormatDaysAndHoursLeft(CountdownLogic.CalculateHoursLeft(targetDate, now), culture),
                CountdownHourDisplayMode.NearDeadline when daysLeft <= preferences.HoursDisplayThresholdDays =>
                    FormatHoursLeft(CountdownLogic.CalculateHoursLeft(targetDate, now), culture),
                _ => FormatDaysLeft(daysLeft, culture)
            };
        }

        public CountdownDisplayContent FormatCountdownContent(
            DateTimeOffset targetDate,
            DateTimeOffset now,
            CountdownPreferences preferences,
            CultureInfo? culture = null)
        {
            string accessibleText = FormatCountdown(targetDate, now, preferences, culture);
            if (preferences.HourDisplayMode != CountdownHourDisplayMode.DaysAndHours ||
                CountdownLogic.CalculateDaysLeft(targetDate, now) < 0)
            {
                return new CountdownDisplayContent(accessibleText, string.Empty, accessibleText);
            }

            return FormatDaysAndHoursContentLeft(
                CountdownLogic.CalculateHoursLeft(targetDate, now),
                accessibleText,
                culture);
        }

        public string FormatHoursLeft(int hoursLeft, CultureInfo? culture = null)
        {
            if (hoursLeft <= 0)
            {
                return DueTodayText;
            }

            return FormatNumberedText(
                hoursLeft,
                hoursLeft == 1 ? OneHourLeftFormat : ManyHoursLeftFormat,
                hoursLeft == 1 ? FallbackOneHourLeftFormat : FallbackManyHoursLeftFormat,
                culture);
        }

        public string FormatDaysAndHoursLeft(int hoursLeft, CultureInfo? culture = null)
        {
            if (hoursLeft <= 0)
            {
                return DueTodayText;
            }

            if (hoursLeft < 24)
            {
                return FormatHoursLeft(hoursLeft, culture);
            }

            int days = hoursLeft / 24;
            int hours = hoursLeft % 24;
            if (hours == 0)
            {
                return FormatDaysLeft(days, culture);
            }

            string format = SelectDaysAndHoursFormat(days, hours);
            string fallbackFormat = SelectFallbackDaysAndHoursFormat(days, hours);
            return FormatTwoNumberedText(days, hours, format, fallbackFormat, culture);
        }

        private CountdownDisplayContent FormatDaysAndHoursContentLeft(int hoursLeft, string accessibleText, CultureInfo? culture)
        {
            if (hoursLeft <= 0)
            {
                return new CountdownDisplayContent(DueTodayText, string.Empty, DueTodayText);
            }

            if (hoursLeft < 24)
            {
                string hoursText = FormatHoursLeft(hoursLeft, culture);
                return new CountdownDisplayContent(hoursText, string.Empty, hoursText);
            }

            int days = hoursLeft / 24;
            int hours = hoursLeft % 24;
            if (hours == 0)
            {
                string daysText = FormatDaysLeft(days, culture);
                return new CountdownDisplayContent(daysText, string.Empty, daysText);
            }

            string daysDurationText = FormatNumberedText(
                days,
                days == 1 ? OneDayDurationFormat : ManyDaysDurationFormat,
                days == 1 ? FallbackOneDayDurationFormat : FallbackManyDaysDurationFormat,
                culture);
            string hoursDurationText = FormatNumberedText(
                hours,
                hours == 1 ? OneHourDurationFormat : ManyHoursDurationFormat,
                hours == 1 ? FallbackOneHourDurationFormat : FallbackManyHoursDurationFormat,
                culture);
            return new CountdownDisplayContent(
                daysDurationText,
                hoursDurationText,
                accessibleText,
                CountdownDisplayLayoutKind.DaysAndHours);
        }

        private static string FormatNumberedText(int value, string format, string fallbackFormat, CultureInfo? culture)
        {
            try
            {
                return string.Format(culture ?? CultureInfo.CurrentCulture, format, value);
            }
            catch (FormatException)
            {
                return string.Format(culture ?? CultureInfo.CurrentCulture, fallbackFormat, value);
            }
        }

        private static string FormatTwoNumberedText(int firstValue, int secondValue, string format, string fallbackFormat, CultureInfo? culture)
        {
            try
            {
                return string.Format(culture ?? CultureInfo.CurrentCulture, format, firstValue, secondValue);
            }
            catch (FormatException)
            {
                return string.Format(culture ?? CultureInfo.CurrentCulture, fallbackFormat, firstValue, secondValue);
            }
        }

        private string SelectDaysAndHoursFormat(int days, int hours)
        {
            return (days == 1, hours == 1) switch
            {
                (true, true) => OneDayOneHourLeftFormat,
                (true, false) => OneDayManyHoursLeftFormat,
                (false, true) => ManyDaysOneHourLeftFormat,
                _ => ManyDaysManyHoursLeftFormat
            };
        }

        private static string SelectFallbackDaysAndHoursFormat(int days, int hours)
        {
            return (days == 1, hours == 1) switch
            {
                (true, true) => FallbackOneDayOneHourLeftFormat,
                (true, false) => FallbackOneDayManyHoursLeftFormat,
                (false, true) => FallbackManyDaysOneHourLeftFormat,
                _ => FallbackManyDaysManyHoursLeftFormat
            };
        }

        private static string UseFallback(string? value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value!;
        }
    }
}
