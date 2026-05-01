using System;

namespace DateCountdown.Core
{
    public sealed class DesktopOverlaySettings
    {
        public DesktopOverlaySettings(
            string? countdownId,
            bool isEnabled = false,
            double positionX = CountdownPreferences.DefaultDesktopCountdownX,
            double positionY = CountdownPreferences.DefaultDesktopCountdownY,
            double scale = CountdownPreferences.DefaultDesktopCountdownScale,
            string? css = null)
        {
            CountdownId = NormalizeCountdownId(countdownId);
            IsEnabled = isEnabled;
            PositionX = CountdownPreferences.CoerceDesktopCountdownCoordinate(positionX, CountdownPreferences.DefaultDesktopCountdownX);
            PositionY = CountdownPreferences.CoerceDesktopCountdownCoordinate(positionY, CountdownPreferences.DefaultDesktopCountdownY);
            Scale = CountdownPreferences.CoerceDesktopCountdownScale(scale);
            Css = CountdownPreferences.CoerceDesktopCountdownCss(css);
        }

        public string CountdownId { get; }

        public bool IsEnabled { get; }

        public double PositionX { get; }

        public double PositionY { get; }

        public double Scale { get; }

        public string Css { get; }

        public bool ShouldPersist =>
            IsEnabled ||
            Math.Abs(PositionX - CountdownPreferences.DefaultDesktopCountdownX) > 0.0001 ||
            Math.Abs(PositionY - CountdownPreferences.DefaultDesktopCountdownY) > 0.0001 ||
            Math.Abs(Scale - CountdownPreferences.DefaultDesktopCountdownScale) > 0.0001 ||
            !string.Equals(Css, CountdownPreferences.DefaultDesktopCountdownCss, StringComparison.Ordinal);

        public DesktopOverlaySettings With(
            bool? isEnabled = null,
            double? positionX = null,
            double? positionY = null,
            double? scale = null,
            string? css = null)
        {
            return new DesktopOverlaySettings(
                CountdownId,
                isEnabled ?? IsEnabled,
                positionX ?? PositionX,
                positionY ?? PositionY,
                scale ?? Scale,
                css ?? Css);
        }

        private static string NormalizeCountdownId(string? countdownId)
        {
            return string.IsNullOrWhiteSpace(countdownId)
                ? CountdownItem.DefaultId
                : countdownId.Trim();
        }
    }
}
