using System;

namespace DateCountdown.Core
{
    public static class CountdownMainContentLayout
    {
        public const double MinimumTopPadding = 36;
        public const double SelectorMinimumTopPadding = 92;
        public const double DefaultBottomPadding = 40;
        public const double EditorBottomPadding = 24;

        public static MainContentPadding CalculatePadding(
            double viewportHeight,
            double countdownSurfaceHeight,
            double editorHeight,
            double toolbarHeight,
            double spacing,
            bool isSelectorVisible,
            bool isEditorVisible)
        {
            double safeViewportHeight = Math.Max(0, viewportHeight);
            double safeCountdownSurfaceHeight = Math.Max(0, countdownSurfaceHeight);
            double safeEditorHeight = Math.Max(0, editorHeight);
            double safeToolbarHeight = Math.Max(0, toolbarHeight);
            double safeSpacing = Math.Max(0, spacing);
            double bottomPadding = isEditorVisible ? EditorBottomPadding : DefaultBottomPadding;
            int visibleBlockCount = 0;
            if (safeCountdownSurfaceHeight > 0)
            {
                visibleBlockCount++;
            }

            if (isEditorVisible && safeEditorHeight > 0)
            {
                visibleBlockCount++;
            }

            if (safeToolbarHeight > 0)
            {
                visibleBlockCount++;
            }

            double gapHeight = Math.Max(0, visibleBlockCount - 1) * safeSpacing;
            double editorBlockHeight = isEditorVisible ? safeEditorHeight : 0;
            double visibleContentHeight = safeCountdownSurfaceHeight + editorBlockHeight + safeToolbarHeight + gapHeight;
            double centeredTopPadding = Math.Floor((safeViewportHeight - visibleContentHeight - bottomPadding) / 2);
            double minimumTopPadding = isSelectorVisible ? SelectorMinimumTopPadding : MinimumTopPadding;

            return new MainContentPadding(Math.Max(minimumTopPadding, centeredTopPadding), bottomPadding);
        }

        public static double CalculatePrimaryDurationTextSize(
            double preferredTextSize,
            CountdownDisplayLayoutKind layoutKind)
        {
            double safeTextSize = double.IsFinite(preferredTextSize) ? Math.Max(0, preferredTextSize) : 0;
            return layoutKind == CountdownDisplayLayoutKind.DaysAndHours
                ? Math.Round(safeTextSize * 0.9)
                : safeTextSize;
        }

        public static double CalculateSecondaryDurationTextSize(
            double preferredTextSize,
            CountdownDisplayLayoutKind layoutKind)
        {
            double safeTextSize = double.IsFinite(preferredTextSize) ? Math.Max(0, preferredTextSize) : 0;
            return layoutKind == CountdownDisplayLayoutKind.DaysAndHours
                ? Math.Round(safeTextSize * 0.48)
                : 1;
        }

        public static double CalculateMainContentMaxWidth(
            double preferredTextSize,
            CountdownDisplayLayoutKind layoutKind)
        {
            double safeTextSize = double.IsFinite(preferredTextSize) ? Math.Max(0, preferredTextSize) : 0;
            double widthFactor = layoutKind == CountdownDisplayLayoutKind.DaysAndHours ? 6.2 : 5.4;
            double maximumWidth = layoutKind == CountdownDisplayLayoutKind.DaysAndHours ? 640 : 560;
            return Math.Clamp(safeTextSize * widthFactor, 360, maximumWidth);
        }
    }

    public readonly record struct MainContentPadding(double Top, double Bottom);
}
