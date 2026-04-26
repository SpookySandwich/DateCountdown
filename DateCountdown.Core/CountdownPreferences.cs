using System;

namespace DateCountdown.Core
{
    public sealed class CountdownPreferences
    {
        public static readonly CountdownPreferences Default = new();

        public CountdownPreferences(
            bool sortCountdownsByDaysLeft = false,
            CountdownDisplaySize displaySize = CountdownDisplaySize.Default,
            bool openWindowAtStartup = false)
        {
            SortCountdownsByDaysLeft = sortCountdownsByDaysLeft;
            DisplaySize = displaySize;
            OpenWindowAtStartup = openWindowAtStartup;
        }

        public bool SortCountdownsByDaysLeft { get; }

        public CountdownDisplaySize DisplaySize { get; }

        public bool OpenWindowAtStartup { get; }

        public CountdownPreferences With(
            bool? sortCountdownsByDaysLeft = null,
            CountdownDisplaySize? displaySize = null,
            bool? openWindowAtStartup = null)
        {
            return new CountdownPreferences(
                sortCountdownsByDaysLeft ?? SortCountdownsByDaysLeft,
                displaySize ?? DisplaySize,
                openWindowAtStartup ?? OpenWindowAtStartup);
        }

        public static CountdownDisplaySize ParseDisplaySize(object? value)
        {
            return value is string text &&
                Enum.TryParse(text, ignoreCase: true, out CountdownDisplaySize displaySize)
                    ? displaySize
                    : CountdownDisplaySize.Default;
        }
    }
}
