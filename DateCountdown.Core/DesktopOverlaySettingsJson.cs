using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace DateCountdown.Core
{
    public static class DesktopOverlaySettingsJson
    {
        private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        public static string Serialize(IEnumerable<DesktopOverlaySettings?>? overlays)
        {
            List<StoredDesktopOverlaySettings> storedItems = new List<StoredDesktopOverlaySettings>();
            foreach (DesktopOverlaySettings? overlay in overlays ?? Enumerable.Empty<DesktopOverlaySettings?>())
            {
                if (overlay is null || !overlay.ShouldPersist)
                {
                    continue;
                }

                storedItems.Add(new StoredDesktopOverlaySettings
                {
                    CountdownId = overlay.CountdownId,
                    IsEnabled = overlay.IsEnabled,
                    PositionX = overlay.PositionX,
                    PositionY = overlay.PositionY,
                    Scale = overlay.Scale,
                    Css = overlay.Css
                });
            }

            return JsonSerializer.Serialize(storedItems, SerializerOptions);
        }

        public static IReadOnlyList<DesktopOverlaySettings> Deserialize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return Array.Empty<DesktopOverlaySettings>();
            }

            try
            {
                List<StoredDesktopOverlaySettings>? storedItems = JsonSerializer.Deserialize<List<StoredDesktopOverlaySettings>>(value, SerializerOptions);
                if (storedItems is null)
                {
                    return Array.Empty<DesktopOverlaySettings>();
                }

                List<DesktopOverlaySettings> overlays = new List<DesktopOverlaySettings>();
                HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
                foreach (StoredDesktopOverlaySettings storedItem in storedItems)
                {
                    if (string.IsNullOrWhiteSpace(storedItem.CountdownId) ||
                        !ids.Add(storedItem.CountdownId.Trim()))
                    {
                        continue;
                    }

                    overlays.Add(new DesktopOverlaySettings(
                        storedItem.CountdownId,
                        storedItem.IsEnabled,
                        storedItem.PositionX,
                        storedItem.PositionY,
                        storedItem.Scale,
                        storedItem.Css));
                }

                return overlays;
            }
            catch (JsonException)
            {
                return Array.Empty<DesktopOverlaySettings>();
            }
            catch (ArgumentException)
            {
                return Array.Empty<DesktopOverlaySettings>();
            }
        }

        private sealed class StoredDesktopOverlaySettings
        {
            public string? CountdownId { get; set; }

            public bool IsEnabled { get; set; }

            public double PositionX { get; set; } = CountdownPreferences.DefaultDesktopCountdownX;

            public double PositionY { get; set; } = CountdownPreferences.DefaultDesktopCountdownY;

            public double Scale { get; set; } = CountdownPreferences.DefaultDesktopCountdownScale;

            public string? Css { get; set; }
        }
    }
}
