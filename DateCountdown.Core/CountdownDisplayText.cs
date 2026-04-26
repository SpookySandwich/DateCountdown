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

        public string FormatDaysLeft(int daysLeft, CultureInfo? culture = null)
        {
            string format = daysLeft == 1 ? OneDayLeftFormat : ManyDaysLeftFormat;
            return string.Format(culture ?? CultureInfo.CurrentCulture, format, daysLeft);
        }

        private static string UseFallback(string? value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value!;
        }
    }
}
