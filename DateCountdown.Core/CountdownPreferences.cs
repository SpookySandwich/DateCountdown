using System;

namespace DateCountdown.Core
{
    public sealed class CountdownPreferences
    {
        public const double MinDaysTextSize = 48;
        public const double DefaultDaysTextSize = 72;
        public const double MaxDaysTextSize = 112;
        public const double MinTitleTextSize = 12;
        public const double DefaultTitleTextSize = 14;
        public const double MaxTitleTextSize = 48;

        public static readonly CountdownPreferences Default = new();

        public CountdownPreferences(
            bool sortCountdownsByDaysLeft = false,
            double daysTextSize = DefaultDaysTextSize,
            double titleTextSize = DefaultTitleTextSize,
            bool openWindowAtStartup = false)
        {
            SortCountdownsByDaysLeft = sortCountdownsByDaysLeft;
            DaysTextSize = CoerceDaysTextSize(daysTextSize);
            TitleTextSize = CoerceTitleTextSize(titleTextSize);
            OpenWindowAtStartup = openWindowAtStartup;
        }

        public bool SortCountdownsByDaysLeft { get; }

        public double DaysTextSize { get; }

        public double TitleTextSize { get; }

        public bool OpenWindowAtStartup { get; }

        public CountdownPreferences With(
            bool? sortCountdownsByDaysLeft = null,
            double? daysTextSize = null,
            double? titleTextSize = null,
            bool? openWindowAtStartup = null)
        {
            return new CountdownPreferences(
                sortCountdownsByDaysLeft ?? SortCountdownsByDaysLeft,
                daysTextSize ?? DaysTextSize,
                titleTextSize ?? TitleTextSize,
                openWindowAtStartup ?? OpenWindowAtStartup);
        }

        public static double CoerceDaysTextSize(double value)
        {
            return CoerceTextSize(value, MinDaysTextSize, MaxDaysTextSize, DefaultDaysTextSize);
        }

        public static double CoerceTitleTextSize(double value)
        {
            return CoerceTextSize(value, MinTitleTextSize, MaxTitleTextSize, DefaultTitleTextSize);
        }

        public static (double DaysTextSize, double TitleTextSize) GetLegacyTextSizes(CountdownDisplaySize displaySize)
        {
            return displaySize switch
            {
                CountdownDisplaySize.Compact => (60, 13),
                CountdownDisplaySize.Large => (84, 16),
                _ => (DefaultDaysTextSize, DefaultTitleTextSize)
            };
        }

        public static CountdownDisplaySize ParseDisplaySize(object? value)
        {
            return value is string text &&
                Enum.TryParse(text, ignoreCase: true, out CountdownDisplaySize displaySize)
                    ? displaySize
                    : CountdownDisplaySize.Default;
        }

        private static double CoerceTextSize(double value, double minimum, double maximum, double fallback)
        {
            return double.IsFinite(value)
                ? Math.Clamp(Math.Round(value), minimum, maximum)
                : fallback;
        }
    }
}
