using DateCountdown.Core;
using Xunit;

namespace DateCountdown.Tests;

public sealed class DesktopOverlayLayoutTests
{
    [Fact]
    public void EstimateTextBoundsWidth_ShrinksWithOverlayScale()
    {
        int fullSizeWidth = DesktopOverlayLayout.EstimateTextBoundsWidth(
            availableWidth: 1920,
            daysLeft: "-2 days left",
            title: "CCS",
            daysTextSize: 72,
            titleTextSize: 14,
            scale: 1);

        int smallSizeWidth = DesktopOverlayLayout.EstimateTextBoundsWidth(
            availableWidth: 1920,
            daysLeft: "-2 days left",
            title: "CCS",
            daysTextSize: 72,
            titleTextSize: 14,
            scale: CountdownPreferences.MinDesktopCountdownScale);

        Assert.True(smallSizeWidth < fullSizeWidth);
        Assert.True(smallSizeWidth < 280);
        Assert.True(smallSizeWidth >= DesktopOverlayLayout.MinimumAdjustmentBoxSize);
    }

    [Fact]
    public void EstimateTextBoundsWidth_ClampsToAvailableDesktopWidth()
    {
        int width = DesktopOverlayLayout.EstimateTextBoundsWidth(
            availableWidth: 160,
            daysLeft: "A very long countdown label that cannot fit",
            title: "A very long title",
            daysTextSize: 112,
            titleTextSize: 48,
            scale: 4);

        Assert.Equal(128, width);
    }

    [Fact]
    public void SelectResizeScaleRatio_CornerResizeUsesDominantInwardAxis()
    {
        double ratio = DesktopOverlayLayout.SelectResizeScaleRatio(
            horizontal: true,
            vertical: true,
            widthRatio: 0.45,
            heightRatio: 0.98);

        Assert.Equal(0.45, ratio, precision: 3);
    }

    [Fact]
    public void SelectResizeScaleRatio_CornerResizeUsesDominantOutwardAxis()
    {
        double ratio = DesktopOverlayLayout.SelectResizeScaleRatio(
            horizontal: true,
            vertical: true,
            widthRatio: 1.02,
            heightRatio: 1.4);

        Assert.Equal(1.4, ratio, precision: 3);
    }

    [Fact]
    public void SelectResizeScaleRatio_HorizontalResizeUsesWidth()
    {
        double ratio = DesktopOverlayLayout.SelectResizeScaleRatio(
            horizontal: true,
            vertical: false,
            widthRatio: 0.6,
            heightRatio: 1);

        Assert.Equal(0.6, ratio, precision: 3);
    }

    [Fact]
    public void SelectResizeScaleRatio_VerticalResizeUsesHeight()
    {
        double ratio = DesktopOverlayLayout.SelectResizeScaleRatio(
            horizontal: false,
            vertical: true,
            widthRatio: 1,
            heightRatio: 0.7);

        Assert.Equal(0.7, ratio, precision: 3);
    }
}
