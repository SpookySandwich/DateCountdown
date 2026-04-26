using System;

namespace DateCountdown.Core
{
    public sealed class CountdownState
    {
        public CountdownState(string title, DateTimeOffset targetDate, bool tileEnabled, bool toastEnabled)
        {
            Title = title ?? string.Empty;
            TargetDate = targetDate;
            TileEnabled = tileEnabled;
            ToastEnabled = toastEnabled;
        }

        public string Title { get; }

        public DateTimeOffset TargetDate { get; }

        public bool TileEnabled { get; }

        public bool ToastEnabled { get; }

        public static CountdownState CreateDefault(DateTimeOffset targetDate)
        {
            return new CountdownState(string.Empty, targetDate, false, false);
        }

        public CountdownState With(string? title = null, DateTimeOffset? targetDate = null, bool? tileEnabled = null, bool? toastEnabled = null)
        {
            return new CountdownState(
                title ?? Title,
                targetDate ?? TargetDate,
                tileEnabled ?? TileEnabled,
                toastEnabled ?? ToastEnabled);
        }
    }
}
