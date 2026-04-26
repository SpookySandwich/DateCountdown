using System;
using System.Globalization;

namespace DateCountdown.Core
{
    public sealed class CountdownDisplayText
    {
        private const string FallbackDefaultTitle = "Date Countdown";
        private const string FallbackOneDayLeftFormat = "{0} day left";
        private const string FallbackManyDaysLeftFormat = "{0} days left";

        public CountdownDisplayText(string? defaultTitle, string? oneDayLeftFormat, string? manyDaysLeftFormat)
        {
            DefaultTitle = UseFallback(defaultTitle, FallbackDefaultTitle);
            OneDayLeftFormat = UseFallback(oneDayLeftFormat, FallbackOneDayLeftFormat);
            ManyDaysLeftFormat = UseFallback(manyDaysLeftFormat, FallbackManyDaysLeftFormat);
        }

        public string DefaultTitle { get; }

        public string OneDayLeftFormat { get; }

        public string ManyDaysLeftFormat { get; }

        public string FormatTitle(string? title)
        {
            string normalizedTitle = CountdownItem.NormalizeTitle(title);
            return string.IsNullOrWhiteSpace(normalizedTitle) ? DefaultTitle : normalizedTitle;
        }

        public string FormatDaysLeft(int daysLeft, CultureInfo? culture = null)
        {
            string format = daysLeft == 1 ? OneDayLeftFormat : ManyDaysLeftFormat;
            try
            {
                return string.Format(culture ?? CultureInfo.CurrentCulture, format, daysLeft);
            }
            catch (FormatException)
            {
                string fallbackFormat = daysLeft == 1 ? FallbackOneDayLeftFormat : FallbackManyDaysLeftFormat;
                return string.Format(culture ?? CultureInfo.CurrentCulture, fallbackFormat, daysLeft);
            }
        }

        private static string UseFallback(string? value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value!;
        }
    }
}
