using System;
using System.Collections.Generic;
using System.Globalization;
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

        public static string SerializeCountdowns(IEnumerable<CountdownItem?>? countdowns)
        {
            List<StoredCountdownItem> storedItems = new List<StoredCountdownItem>();
            if (countdowns is null)
            {
                return JsonSerializer.Serialize(storedItems, SerializerOptions);
            }

            foreach (CountdownItem? countdown in countdowns)
            {
                if (countdown is null)
                {
                    continue;
                }

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
                using JsonDocument document = JsonDocument.Parse(value);
                if (document.RootElement.ValueKind != JsonValueKind.Array)
                {
                    return Array.Empty<CountdownItem>();
                }

                List<CountdownItem> countdowns = new List<CountdownItem>();
                foreach (JsonElement storedItem in document.RootElement.EnumerateArray())
                {
                    if (TryReadCountdown(storedItem, out CountdownItem? countdown) && countdown is not null)
                    {
                        countdowns.Add(countdown);
                    }
                }

                return countdowns;
            }
            catch (JsonException)
            {
                return Array.Empty<CountdownItem>();
            }
            catch (ArgumentException)
            {
                return Array.Empty<CountdownItem>();
            }
        }

        private static bool TryReadCountdown(JsonElement storedItem, out CountdownItem? countdown)
        {
            countdown = null;
            if (storedItem.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            string? id = TryGetString(storedItem, "id");
            if (string.IsNullOrWhiteSpace(id))
            {
                return false;
            }

            if (!TryGetDateTimeOffset(storedItem, "targetDate", out DateTimeOffset targetDate) ||
                targetDate == default)
            {
                return false;
            }

            countdown = new CountdownItem(
                id,
                TryGetString(storedItem, "title"),
                targetDate,
                TryGetBool(storedItem, "toastEnabled"));
            return true;
        }

        private static string? TryGetString(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out JsonElement property) ||
                property.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            return property.GetString();
        }

        private static bool TryGetBool(JsonElement element, string propertyName)
        {
            return element.TryGetProperty(propertyName, out JsonElement property) &&
                (property.ValueKind switch
                {
                    JsonValueKind.True => true,
                    _ => false
                });
        }

        private static bool TryGetDateTimeOffset(JsonElement element, string propertyName, out DateTimeOffset value)
        {
            value = default;
            if (!element.TryGetProperty(propertyName, out JsonElement property))
            {
                return false;
            }

            if (property.ValueKind == JsonValueKind.String)
            {
                return property.TryGetDateTimeOffset(out value) ||
                    DateTimeOffset.TryParse(
                        property.GetString(),
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeLocal,
                        out value);
            }

            return false;
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
