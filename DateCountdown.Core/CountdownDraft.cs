using System;

namespace DateCountdown.Core
{
    public sealed class CountdownDraft
    {
        public CountdownDraft(
            string? title,
            DateTimeOffset? targetDate,
            bool tileEnabled,
            bool toastEnabled,
            string? targetTimeZoneId = null)
        {
            Title = CountdownItem.NormalizeTitle(title);
            TargetDate = targetDate;
            TileEnabled = tileEnabled;
            ToastEnabled = toastEnabled;
            TargetTimeZoneId = CountdownTimeZone.NormalizeTimeZoneId(targetTimeZoneId);
        }

        public string Title { get; }

        public DateTimeOffset? TargetDate { get; }

        public bool TileEnabled { get; }

        public bool ToastEnabled { get; }

        public string TargetTimeZoneId { get; }

        public bool CanCommit(DateTimeOffset now)
        {
            return TargetDate.HasValue &&
                TargetDate.Value.ToUniversalTime() >= now.ToUniversalTime() &&
                !string.IsNullOrWhiteSpace(Title);
        }

        public bool TryCommit(DateTimeOffset now, out CountdownState? state)
        {
            if (!CanCommit(now))
            {
                state = null;
                return false;
            }

            state = new CountdownState(Title, TargetDate!.Value, TileEnabled, ToastEnabled, TargetTimeZoneId);
            return true;
        }

        public bool TryApplyTo(CountdownState currentState, DateTimeOffset now, out CountdownState? state)
        {
            if (!CanCommit(now))
            {
                state = null;
                return false;
            }

            state = currentState.With(
                title: Title,
                targetDate: TargetDate!.Value,
                tileEnabled: TileEnabled,
                toastEnabled: ToastEnabled,
                targetTimeZoneId: TargetTimeZoneId);
            return true;
        }
    }
}
