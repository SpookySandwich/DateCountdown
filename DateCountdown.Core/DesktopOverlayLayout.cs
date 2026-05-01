using System;
using System.Globalization;

namespace DateCountdown.Core
{
    public static class DesktopOverlayLayout
    {
        public const int MinimumAdjustmentBoxSize = 96;

        private const int ContentHorizontalPadding = 48;
        private const double DaysWidthFactor = 0.58;
        private const double TitleWidthFactor = 0.72;
        private const double MinimumResizeRatio = 0.1;

        public static int EstimateTextBoundsWidth(
            int availableWidth,
            string? daysLeft,
            string? title,
            double daysTextSize,
            double titleTextSize,
            double scale)
        {
            int maximum = Math.Max(MinimumAdjustmentBoxSize, availableWidth - 32);
            double safeScale = CountdownPreferences.CoerceDesktopCountdownScale(scale);
            double daysWidth = EstimateTextWidth(daysLeft, daysTextSize * safeScale, DaysWidthFactor);
            double titleWidth = EstimateTextWidth(title, titleTextSize * safeScale, TitleWidthFactor);
            int width = (int)Math.Ceiling(Math.Max(daysWidth, titleWidth) + ContentHorizontalPadding);
            return Math.Clamp(width, MinimumAdjustmentBoxSize, maximum);
        }

        public static double SelectResizeScaleRatio(bool horizontal, bool vertical, double widthRatio, double heightRatio)
        {
            double safeWidthRatio = Math.Max(MinimumResizeRatio, widthRatio);
            double safeHeightRatio = Math.Max(MinimumResizeRatio, heightRatio);
            return (horizontal, vertical) switch
            {
                (true, true) => Math.Abs(safeWidthRatio - 1) >= Math.Abs(safeHeightRatio - 1)
                    ? safeWidthRatio
                    : safeHeightRatio,
                (true, false) => safeWidthRatio,
                (false, true) => safeHeightRatio,
                _ => 1
            };
        }

        private static double EstimateTextWidth(string? text, double fontSize, double widthFactor)
        {
            string value = text ?? string.Empty;
            int textElementCount = new StringInfo(value).LengthInTextElements;
            return textElementCount * Math.Max(1, fontSize) * widthFactor;
        }
    }
}
