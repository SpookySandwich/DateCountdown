using DateCountdown.Core;
using Xunit;

namespace DateCountdown.Tests;

public sealed class CountdownMainContentLayoutTests
{
    [Fact]
    public void CalculatePadding_NewCountdownEditorWithoutCountdownSurface_CentersEditor()
    {
        MainContentPadding padding = CountdownMainContentLayout.CalculatePadding(
            viewportHeight: 900,
            countdownSurfaceHeight: 0,
            editorHeight: 240,
            toolbarHeight: 40,
            spacing: 26,
            isSelectorVisible: false,
            isEditorVisible: true);

        Assert.Equal(285, padding.Top);
        Assert.Equal(CountdownMainContentLayout.EditorBottomPadding, padding.Bottom);
    }

    [Fact]
    public void CalculatePadding_ExistingCountdownEditorIncludesCountdownSurface()
    {
        MainContentPadding padding = CountdownMainContentLayout.CalculatePadding(
            viewportHeight: 900,
            countdownSurfaceHeight: 140,
            editorHeight: 240,
            toolbarHeight: 40,
            spacing: 26,
            isSelectorVisible: false,
            isEditorVisible: true);

        Assert.Equal(202, padding.Top);
        Assert.Equal(CountdownMainContentLayout.EditorBottomPadding, padding.Bottom);
    }

    [Fact]
    public void CalculatePadding_SelectorVisibleReservesTopRailSpace()
    {
        MainContentPadding padding = CountdownMainContentLayout.CalculatePadding(
            viewportHeight: 300,
            countdownSurfaceHeight: 140,
            editorHeight: 0,
            toolbarHeight: 40,
            spacing: 26,
            isSelectorVisible: true,
            isEditorVisible: false);

        Assert.Equal(CountdownMainContentLayout.SelectorMinimumTopPadding, padding.Top);
        Assert.Equal(CountdownMainContentLayout.DefaultBottomPadding, padding.Bottom);
    }

    [Fact]
    public void CalculatePrimaryDurationTextSize_WithSimpleDuration_UsesPreferredSize()
    {
        Assert.Equal(
            72,
            CountdownMainContentLayout.CalculatePrimaryDurationTextSize(
                72,
                CountdownDisplayLayoutKind.SingleLine));
    }

    [Fact]
    public void CalculatePrimaryDurationTextSize_WithCompoundDuration_ScalesPreferredSize()
    {
        Assert.Equal(
            65,
            CountdownMainContentLayout.CalculatePrimaryDurationTextSize(
                72,
                CountdownDisplayLayoutKind.DaysAndHours));
    }

    [Fact]
    public void CalculateSecondaryDurationTextSize_WithCompoundDuration_UsesSubordinateSize()
    {
        Assert.Equal(
            35,
            CountdownMainContentLayout.CalculateSecondaryDurationTextSize(
                72,
                CountdownDisplayLayoutKind.DaysAndHours));
    }

    [Fact]
    public void CalculateSecondaryDurationTextSize_WithSimpleDuration_KeepsValidFontSize()
    {
        Assert.Equal(
            1,
            CountdownMainContentLayout.CalculateSecondaryDurationTextSize(
                72,
                CountdownDisplayLayoutKind.SingleLine));
    }

    [Fact]
    public void CalculateMainContentMaxWidth_WithCompoundDuration_AllowsModerateWidth()
    {
        Assert.Equal(
            446.4,
            CountdownMainContentLayout.CalculateMainContentMaxWidth(
                72,
                CountdownDisplayLayoutKind.DaysAndHours),
            1);
    }
}
