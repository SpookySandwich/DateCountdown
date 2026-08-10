using System;
using System.Collections.Generic;

namespace DateCountdown.Core
{
    public static class CountdownRefreshSchedule
    {
        private static readonly TimeSpan PostBoundaryRefreshBuffer = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan MinimumRefreshDelay = TimeSpan.FromSeconds(1);

        public static TimeSpan GetDelayUntilNextCountdownRefresh(DateTimeOffset now, CountdownPreferences preferences)
        {
            return preferences.HourDisplayMode == CountdownHourDisplayMode.DaysOnly
                ? GetDelayUntilNextLocalDateRefresh(now)
                : GetDelayUntilNextLocalHourRefresh(now);
        }

        public static TimeSpan GetDelayUntilNextCountdownRefresh(
            DateTimeOffset now,
            CountdownPreferences preferences,
            IEnumerable<CountdownItem> countdowns)
        {
            TimeSpan scheduledDelay = GetDelayUntilNextCountdownRefresh(now, preferences);
            foreach (CountdownItem countdown in countdowns)
            {
                TimeSpan targetBoundaryDelay = GetDelayUntilNextTargetBoundary(now, preferences, countdown);
                if (targetBoundaryDelay > TimeSpan.Zero && targetBoundaryDelay < scheduledDelay)
                {
                    scheduledDelay = targetBoundaryDelay;
                }
            }

            return scheduledDelay > TimeSpan.Zero
                ? scheduledDelay
                : MinimumRefreshDelay;
        }

        public static TimeSpan GetDelayUntilNextLocalDateRefresh(DateTimeOffset now)
        {
            DateTime nextLocalDate = now.Date.AddDays(1);
            TimeSpan delay = nextLocalDate - now.DateTime;
            return delay > TimeSpan.Zero
                ? delay + PostBoundaryRefreshBuffer
                : MinimumRefreshDelay;
        }

        public static TimeSpan GetDelayUntilNextLocalHourRefresh(DateTimeOffset now)
        {
            DateTime localNow = now.DateTime;
            DateTime nextLocalHour = new(localNow.Year, localNow.Month, localNow.Day, localNow.Hour, 0, 0);
            TimeSpan delay = nextLocalHour.AddHours(1) - localNow;
            return delay > TimeSpan.Zero
                ? delay + PostBoundaryRefreshBuffer
                : MinimumRefreshDelay;
        }

        private static TimeSpan GetDelayUntilNextTargetBoundary(
            DateTimeOffset now,
            CountdownPreferences preferences,
            CountdownItem countdown)
        {
            TimeSpan remaining = countdown.TargetDate.ToUniversalTime() - now.ToUniversalTime();
            bool usesHours = remaining > TimeSpan.Zero &&
                (preferences.HourDisplayMode == CountdownHourDisplayMode.HoursOnly ||
                    preferences.HourDisplayMode == CountdownHourDisplayMode.DaysAndHours ||
                    preferences.HourDisplayMode == CountdownHourDisplayMode.NearDeadline &&
                    CountdownLogic.CalculateDaysLeft(countdown.TargetDate, now) <= preferences.HoursDisplayThresholdDays);
            TimeSpan interval = usesHours ? TimeSpan.FromHours(1) : TimeSpan.FromDays(1);

            return GetDelayUntilNextTargetRelativeBoundary(remaining, interval);
        }

        private static TimeSpan GetDelayUntilNextTargetRelativeBoundary(TimeSpan remaining, TimeSpan interval)
        {
            if (remaining == TimeSpan.Zero)
            {
                return PostBoundaryRefreshBuffer;
            }

            if (remaining > TimeSpan.Zero)
            {
                long unitsLeft = (long)Math.Ceiling(remaining.Ticks / (double)interval.Ticks);
                TimeSpan delay = remaining - TimeSpan.FromTicks(interval.Ticks * (unitsLeft - 1));
                return delay > TimeSpan.Zero
                    ? delay + PostBoundaryRefreshBuffer
                    : MinimumRefreshDelay;
            }

            TimeSpan elapsed = remaining.Duration();
            long elapsedUnits = (long)Math.Ceiling(elapsed.Ticks / (double)interval.Ticks);
            TimeSpan overdueBoundaryDelay = TimeSpan.FromTicks(interval.Ticks * elapsedUnits) - elapsed;
            return overdueBoundaryDelay >= TimeSpan.Zero
                ? overdueBoundaryDelay + PostBoundaryRefreshBuffer
                : MinimumRefreshDelay;
        }
    }
}
