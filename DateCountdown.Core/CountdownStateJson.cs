using System;
using System.Collections.Generic;
using System.Text.Json;

namespace DateCountdown.Core
{
    public static class CountdownStateJson
    {
        private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        public static string SerializeCountdowns(IEnumerable<CountdownItem> countdowns)
        {
            List<StoredCountdownItem> storedItems = new List<StoredCountdownItem>();
            foreach (CountdownItem countdown in countdowns)
            {
                storedItems.Add(new StoredCountdownItem
                {
                    Id = countdown.Id,
                    Title = countdown.Title,
                    TargetDate = countdown.TargetDate,
                    ToastEnabled = countdown.ToastEnabled
                });
            }

            return JsonSerializer.Serialize(storedItems, SerializerOptions);
        }

        public static IReadOnlyList<CountdownItem> DeserializeCountdowns(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return Array.Empty<CountdownItem>();
            }

            try
            {
                List<StoredCountdownItem>? storedItems = JsonSerializer.Deserialize<List<StoredCountdownItem>>(value, SerializerOptions);
                if (storedItems is null)
                {
                    return Array.Empty<CountdownItem>();
                }

                List<CountdownItem> countdowns = new List<CountdownItem>();
                foreach (StoredCountdownItem storedItem in storedItems)
                {
                    if (!string.IsNullOrWhiteSpace(storedItem.Id))
                    {
                        countdowns.Add(new CountdownItem(
                            storedItem.Id,
                            storedItem.Title,
                            storedItem.TargetDate,
                            storedItem.ToastEnabled));
                    }
                }

                return countdowns;
            }
            catch (JsonException)
            {
                return Array.Empty<CountdownItem>();
            }
        }

        private sealed class StoredCountdownItem
        {
            public string? Id { get; set; }

            public string? Title { get; set; }

            public DateTimeOffset TargetDate { get; set; }

            public bool ToastEnabled { get; set; }
        }
    }
}
