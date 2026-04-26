using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace DateCountdown.Core
{
    public sealed class CountdownState
    {
        public CountdownState(string title, DateTimeOffset targetDate, bool tileEnabled, bool toastEnabled)
            : this(
                new[] { new CountdownItem(CountdownItem.DefaultId, title, targetDate) },
                CountdownItem.DefaultId,
                tileEnabled,
                toastEnabled)
        {
        }

        public CountdownState(IEnumerable<CountdownItem?>? countdowns, string? selectedCountdownId, bool tileEnabled, bool toastEnabled)
        {
            List<CountdownItem> items = Deduplicate(countdowns).ToList();
            if (items.Count == 0)
            {
                items.Add(new CountdownItem(CountdownItem.DefaultId, string.Empty, DateTimeOffset.Now));
            }

            string resolvedSelectedCountdownId = items.Any(item => string.Equals(item.Id, selectedCountdownId, StringComparison.Ordinal))
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

            Countdowns = new ReadOnlyCollection<CountdownItem>(items);
            SelectedCountdownId = resolvedSelectedCountdownId;
            TileEnabled = tileEnabled;
        }

        public IReadOnlyList<CountdownItem> Countdowns { get; }

        public string SelectedCountdownId { get; }

        public CountdownItem SelectedCountdown
        {
            get
            {
                return Countdowns.FirstOrDefault(item => string.Equals(item.Id, SelectedCountdownId, StringComparison.Ordinal)) ?? Countdowns[0];
            }
        }

        public string Title => SelectedCountdown.Title;

        public DateTimeOffset TargetDate => SelectedCountdown.TargetDate;

        public bool TileEnabled { get; }

        public bool ToastEnabled => SelectedCountdown.ToastEnabled;

        public bool AnyToastEnabled => Countdowns.Any(item => item.ToastEnabled);

        public bool CanRemoveCountdown => Countdowns.Count > 1;

        public static CountdownState CreateDefault(DateTimeOffset targetDate)
        {
            return new CountdownState(string.Empty, targetDate, false, false);
        }

        public CountdownState With(string? title = null, DateTimeOffset? targetDate = null, bool? tileEnabled = null, bool? toastEnabled = null)
        {
            CountdownItem selectedCountdown = SelectedCountdown.With(title, targetDate, toastEnabled);
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
                countdown = new CountdownItem(id, countdown.Title, countdown.TargetDate, countdown.ToastEnabled);
            }

            List<CountdownItem> countdowns = Countdowns.ToList();
            countdowns.Add(countdown);

            return new CountdownState(
                countdowns,
                selectCountdown ? countdown.Id : SelectedCountdownId,
                TileEnabled,
                toastEnabled: false);
        }

        public CountdownState SelectCountdown(string countdownId)
        {
            return Countdowns.Any(item => string.Equals(item.Id, countdownId, StringComparison.Ordinal))
                ? new CountdownState(Countdowns, countdownId, TileEnabled, toastEnabled: false)
                : this;
        }

        public CountdownState RemoveCountdown(string countdownId)
        {
            if (!CanRemoveCountdown)
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

            string selectedCountdownId = SelectedCountdownId;
            if (string.Equals(selectedCountdownId, countdownId, StringComparison.Ordinal))
            {
                int selectedIndex = Math.Min(removedIndex, countdowns.Count - 1);
                selectedCountdownId = countdowns[selectedIndex].Id;
            }

            return new CountdownState(countdowns, selectedCountdownId, TileEnabled, toastEnabled: false);
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
