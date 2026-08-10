using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace DateCountdown.Core
{
    public sealed class CountdownState
    {
        public CountdownState(
            string title,
            DateTimeOffset targetDate,
            bool tileEnabled,
            bool toastEnabled,
            string? targetTimeZoneId = null)
            : this(
                new[] { new CountdownItem(CountdownItem.DefaultId, title, targetDate, targetTimeZoneId: targetTimeZoneId) },
                CountdownItem.DefaultId,
                tileEnabled,
                toastEnabled)
        {
        }

        public CountdownState(IEnumerable<CountdownItem?>? countdowns, string? selectedCountdownId, bool tileEnabled, bool toastEnabled)
        {
            List<CountdownItem> items = Deduplicate(countdowns).ToList();

            string resolvedSelectedCountdownId = string.Empty;
            if (items.Count > 0)
            {
                resolvedSelectedCountdownId = items.Any(item => string.Equals(item.Id, selectedCountdownId, StringComparison.Ordinal))
                    ? selectedCountdownId!
                    : items[0].Id;
                if (toastEnabled && !items.Any(item => item.ToastEnabled))
                {
                    items = items
                        .Select(item => string.Equals(item.Id, resolvedSelectedCountdownId, StringComparison.Ordinal)
                            ? item.With(toastEnabled: true)
                            : item)
                        .ToList();
                }
            }

            Countdowns = new ReadOnlyCollection<CountdownItem>(items);
            SelectedCountdownId = resolvedSelectedCountdownId;
            TileEnabled = items.Count > 0 && tileEnabled;
        }

        public IReadOnlyList<CountdownItem> Countdowns { get; }

        public string SelectedCountdownId { get; }

        public bool HasCountdowns => Countdowns.Count > 0;

        public CountdownItem? SelectedCountdown
        {
            get
            {
                return Countdowns.FirstOrDefault(item => string.Equals(item.Id, SelectedCountdownId, StringComparison.Ordinal));
            }
        }

        public string Title => SelectedCountdown?.Title ?? string.Empty;

        public DateTimeOffset TargetDate => SelectedCountdown?.TargetDate ?? default;

        public string TargetTimeZoneId => SelectedCountdown?.TargetTimeZoneId ?? CountdownTimeZone.DefaultTimeZoneId;

        public bool TileEnabled { get; }

        public bool ToastEnabled => SelectedCountdown?.ToastEnabled ?? false;

        public bool AnyToastEnabled => Countdowns.Any(item => item.ToastEnabled);

        public bool CanRemoveCountdown => HasCountdowns;

        public static CountdownState CreateDefault(DateTimeOffset targetDate)
        {
            return CreateEmpty();
        }

        public static CountdownState CreateEmpty()
        {
            return new CountdownState(Array.Empty<CountdownItem>(), string.Empty, false, false);
        }

        public CountdownState With(
            string? title = null,
            DateTimeOffset? targetDate = null,
            bool? tileEnabled = null,
            bool? toastEnabled = null,
            string? targetTimeZoneId = null)
        {
            if (SelectedCountdown is not CountdownItem currentSelectedCountdown)
            {
                return new CountdownState(
                    Countdowns,
                    SelectedCountdownId,
                    tileEnabled ?? TileEnabled,
                    toastEnabled: false);
            }

            CountdownItem selectedCountdown = currentSelectedCountdown.With(title, targetDate, toastEnabled, targetTimeZoneId);
            List<CountdownItem> countdowns = Countdowns
                .Select(item => string.Equals(item.Id, selectedCountdown.Id, StringComparison.Ordinal) ? selectedCountdown : item)
                .ToList();

            return new CountdownState(
                countdowns,
                SelectedCountdownId,
                tileEnabled ?? TileEnabled,
                toastEnabled: false);
        }

        public CountdownState AddCountdown(CountdownItem? countdown, bool selectCountdown)
        {
            if (countdown is null)
            {
                return this;
            }

            string id = countdown.Id;
            if (Countdowns.Any(item => string.Equals(item.Id, id, StringComparison.Ordinal)))
            {
                id = Guid.NewGuid().ToString("N");
                countdown = new CountdownItem(id, countdown.Title, countdown.TargetDate, countdown.ToastEnabled, countdown.TargetTimeZoneId);
            }

            List<CountdownItem> countdowns = Countdowns.ToList();
            countdowns.Add(countdown);

            return new CountdownState(
                countdowns,
                selectCountdown || !HasCountdowns ? countdown.Id : SelectedCountdownId,
                TileEnabled,
                toastEnabled: false);
        }

        public CountdownState AddDefaultCountdown(DateTimeOffset targetDate, Func<int, string> titleFactory)
        {
            ArgumentNullException.ThrowIfNull(titleFactory);
            HashSet<string> existingTitles = CreateExistingTitleSet(Countdowns);

            CountdownItem countdown = new(
                Guid.NewGuid().ToString("N"),
                CreateUniqueDefaultTitle(titleFactory, Countdowns.Count + 1, existingTitles),
                targetDate);

            return AddCountdown(countdown, selectCountdown: true);
        }

        private static HashSet<string> CreateExistingTitleSet(IEnumerable<CountdownItem> countdowns)
        {
            return new HashSet<string>(
                countdowns
                    .Select(countdown => countdown.Title)
                    .Where(title => !string.IsNullOrWhiteSpace(title)),
                StringComparer.CurrentCultureIgnoreCase);
        }

        private static string CreateUniqueDefaultTitle(Func<int, string> titleFactory, int initialNumber, ISet<string> existingTitles)
        {
            int candidateNumber = Math.Max(1, initialNumber);
            for (int attempts = 0; attempts < existingTitles.Count + 1000; attempts++, candidateNumber++)
            {
                string title = CountdownItem.NormalizeTitle(titleFactory(candidateNumber));
                if (!string.IsNullOrWhiteSpace(title) && !existingTitles.Contains(title))
                {
                    existingTitles.Add(title);
                    return title;
                }
            }

            return CountdownItem.NormalizeTitle(titleFactory(Math.Max(1, initialNumber)));
        }

        public CountdownState WithDefaultTitles(Func<int, string> titleFactory)
        {
            ArgumentNullException.ThrowIfNull(titleFactory);

            List<CountdownItem>? countdowns = null;
            HashSet<string> existingTitles = CreateExistingTitleSet(Countdowns);
            for (int index = 0; index < Countdowns.Count; index++)
            {
                CountdownItem countdown = Countdowns[index];
                if (!string.IsNullOrWhiteSpace(countdown.Title))
                {
                    countdowns?.Add(countdown);
                    continue;
                }

                countdowns ??= Countdowns.Take(index).ToList();
                countdowns.Add(countdown.With(title: CreateUniqueDefaultTitle(titleFactory, index + 1, existingTitles)));
            }

            return countdowns is null
                ? this
                : new CountdownState(countdowns, SelectedCountdownId, TileEnabled, toastEnabled: false);
        }

        public CountdownState SelectCountdown(string countdownId)
        {
            return Countdowns.Any(item => string.Equals(item.Id, countdownId, StringComparison.Ordinal))
                ? new CountdownState(Countdowns, countdownId, TileEnabled, toastEnabled: false)
                : this;
        }

        public CountdownState RemoveCountdown(string countdownId)
        {
            if (!HasCountdowns)
            {
                return this;
            }

            int removedIndex = Countdowns
                .Select((item, index) => new { item, index })
                .FirstOrDefault(entry => string.Equals(entry.item.Id, countdownId, StringComparison.Ordinal))?.index ?? -1;

            if (removedIndex < 0)
            {
                return this;
            }

            List<CountdownItem> countdowns = Countdowns
                .Where(item => !string.Equals(item.Id, countdownId, StringComparison.Ordinal))
                .ToList();

            string selectedCountdownId = string.Empty;
            if (countdowns.Count > 0)
            {
                selectedCountdownId = SelectedCountdownId;
                if (string.Equals(selectedCountdownId, countdownId, StringComparison.Ordinal) ||
                    !countdowns.Any(item => string.Equals(item.Id, selectedCountdownId, StringComparison.Ordinal)))
                {
                    int selectedIndex = Math.Min(removedIndex, countdowns.Count - 1);
                    selectedCountdownId = countdowns[selectedIndex].Id;
                }
            }

            return new CountdownState(countdowns, selectedCountdownId, TileEnabled, toastEnabled: false);
        }

        public CountdownState RemoveExpiredCountdowns(DateTimeOffset now)
        {
            List<CountdownItem> activeCountdowns = Countdowns
                .Where(item => !CountdownLogic.IsExpired(item.TargetDate, now))
                .ToList();

            if (activeCountdowns.Count == Countdowns.Count)
            {
                return this;
            }

            if (activeCountdowns.Count == 0)
            {
                return CreateEmpty();
            }

            return new CountdownState(activeCountdowns, SelectedCountdownId, TileEnabled, toastEnabled: false);
        }

        public CountdownState SortByDaysLeft(DateTimeOffset now)
        {
            List<CountdownItem> countdowns = Countdowns
                .OrderBy(item => item.TargetDate.UtcDateTime)
                .ThenBy(item => item.Title, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(item => item.Id, StringComparer.Ordinal)
                .ToList();

            return new CountdownState(countdowns, SelectedCountdownId, TileEnabled, toastEnabled: false);
        }

        private static IEnumerable<CountdownItem> Deduplicate(IEnumerable<CountdownItem?>? countdowns)
        {
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (CountdownItem? countdown in countdowns ?? Enumerable.Empty<CountdownItem?>())
            {
                if (countdown is not null && ids.Add(countdown.Id))
                {
                    yield return countdown;
                }
            }
        }
    }
}
