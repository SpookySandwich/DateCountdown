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
}
