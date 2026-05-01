using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

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
        public const double DefaultDesktopCountdownX = 0.5;
        public const double DefaultDesktopCountdownY = 0.5;
        public const double MinDesktopCountdownScale = 0.25;
        public const double DefaultDesktopCountdownScale = 1;
        public const int MaxDesktopCountdownCssLength = 8000;
        private const string LegacyPlacementCssMarker = "--position-x";
        public const string DefaultDesktopCountdownCss =
            ".countdown {\n" +
            "  color: white;\n" +
            "  font-family: 'Segoe UI Variable Display', 'Segoe UI', sans-serif;\n" +
            "  text-align: center;\n" +
            "  text-shadow: 0 2px 6px rgba(0, 0, 0, 0.78);\n" +
            "}\n\n" +
            ".days {\n" +
            "  font-weight: 300;\n" +
            "  line-height: 1.1;\n" +
            "  white-space: nowrap;\n" +
            "}\n\n" +
            ".title {\n" +
            "  margin-top: 0.25em;\n" +
            "  font-weight: 600;\n" +
            "}";

        public static readonly CountdownPreferences Default = new();

        public CountdownPreferences(
            bool sortCountdownsByDaysLeft = false,
            double daysTextSize = DefaultDaysTextSize,
            double titleTextSize = DefaultTitleTextSize,
            bool openWindowAtStartup = false,
            bool showDesktopCountdown = false,
            bool reduceMotion = false,
            double desktopCountdownX = DefaultDesktopCountdownX,
            double desktopCountdownY = DefaultDesktopCountdownY,
            double desktopCountdownScale = DefaultDesktopCountdownScale,
            string? desktopCountdownCss = null,
            IEnumerable<DesktopOverlaySettings?>? desktopOverlays = null)
        {
            SortCountdownsByDaysLeft = sortCountdownsByDaysLeft;
            DaysTextSize = CoerceDaysTextSize(daysTextSize);
            TitleTextSize = CoerceTitleTextSize(titleTextSize);
            OpenWindowAtStartup = openWindowAtStartup;
            ShowDesktopCountdown = showDesktopCountdown;
            ReduceMotion = reduceMotion;
            DesktopCountdownX = CoerceDesktopCountdownCoordinate(desktopCountdownX, DefaultDesktopCountdownX);
            DesktopCountdownY = CoerceDesktopCountdownCoordinate(desktopCountdownY, DefaultDesktopCountdownY);
            DesktopCountdownScale = CoerceDesktopCountdownScale(desktopCountdownScale);
            DesktopCountdownCss = CoerceDesktopCountdownCss(desktopCountdownCss);
            DesktopOverlays = new ReadOnlyCollection<DesktopOverlaySettings>(NormalizeDesktopOverlays(desktopOverlays).ToList());
        }

        public bool SortCountdownsByDaysLeft { get; }

        public double DaysTextSize { get; }

        public double TitleTextSize { get; }

        public bool OpenWindowAtStartup { get; }

        public bool ShowDesktopCountdown { get; }

        public bool ReduceMotion { get; }

        public double DesktopCountdownX { get; }

        public double DesktopCountdownY { get; }

        public double DesktopCountdownScale { get; }

        public string DesktopCountdownCss { get; }

        public IReadOnlyList<DesktopOverlaySettings> DesktopOverlays { get; }

        public CountdownPreferences With(
            bool? sortCountdownsByDaysLeft = null,
            double? daysTextSize = null,
            double? titleTextSize = null,
            bool? openWindowAtStartup = null,
            bool? showDesktopCountdown = null,
            bool? reduceMotion = null,
            double? desktopCountdownX = null,
            double? desktopCountdownY = null,
            double? desktopCountdownScale = null,
            string? desktopCountdownCss = null,
            IEnumerable<DesktopOverlaySettings?>? desktopOverlays = null)
        {
            return new CountdownPreferences(
                sortCountdownsByDaysLeft ?? SortCountdownsByDaysLeft,
                daysTextSize ?? DaysTextSize,
                titleTextSize ?? TitleTextSize,
                openWindowAtStartup ?? OpenWindowAtStartup,
                showDesktopCountdown ?? ShowDesktopCountdown,
                reduceMotion ?? ReduceMotion,
                desktopCountdownX ?? DesktopCountdownX,
                desktopCountdownY ?? DesktopCountdownY,
                desktopCountdownScale ?? DesktopCountdownScale,
                desktopCountdownCss ?? DesktopCountdownCss,
                desktopOverlays ?? DesktopOverlays);
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

        public static double CoerceDesktopCountdownCoordinate(double value, double fallback)
        {
            return double.IsFinite(value)
                ? Math.Clamp(value, 0, 1)
                : fallback;
        }

        public static double CoerceDesktopCountdownScale(double value)
        {
            return double.IsFinite(value)
                ? Math.Max(value, MinDesktopCountdownScale)
                : DefaultDesktopCountdownScale;
        }

        public static string CoerceDesktopCountdownCss(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return DefaultDesktopCountdownCss;
            }

            if (value.Contains(LegacyPlacementCssMarker, StringComparison.Ordinal))
            {
                return DefaultDesktopCountdownCss;
            }

            return value.Length <= MaxDesktopCountdownCssLength
                ? value
                : value[..MaxDesktopCountdownCssLength];
        }

        private static IEnumerable<DesktopOverlaySettings> NormalizeDesktopOverlays(IEnumerable<DesktopOverlaySettings?>? overlays)
        {
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (DesktopOverlaySettings? overlay in overlays ?? Enumerable.Empty<DesktopOverlaySettings?>())
            {
                if (overlay is not null && ids.Add(overlay.CountdownId) && overlay.ShouldPersist)
                {
                    yield return overlay;
                }
            }
        }
    }
}
