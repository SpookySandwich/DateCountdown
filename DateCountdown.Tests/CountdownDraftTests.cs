using DateCountdown.Core;
using System;
using Xunit;

namespace DateCountdown.Tests;

public sealed class CountdownDraftTests
{
    private static readonly DateTimeOffset Now = new(2026, 4, 25, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Constructor_NormalizesNullTitleToEmptyString()
    {
        CountdownDraft draft = new(null, Now, tileEnabled: false, toastEnabled: false);

        Assert.Equal(string.Empty, draft.Title);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CanCommit_RejectsMissingOrWhitespaceTitles(string? title)
    {
        CountdownDraft draft = new(title, Now, tileEnabled: true, toastEnabled: true);

        Assert.False(draft.CanCommit(Now));
    }

    [Fact]
    public void CanCommit_RejectsMissingDate()
    {
        CountdownDraft draft = new("Launch", null, tileEnabled: false, toastEnabled: false);

        Assert.False(draft.CanCommit(Now));
    }

    [Fact]
    public void CanCommit_RejectsPastDate()
    {
        CountdownDraft draft = new("Launch", Now.AddDays(-1), tileEnabled: false, toastEnabled: false);

        Assert.False(draft.CanCommit(Now));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(30)]
    public void CanCommit_AllowsTodayAndFutureDates(int daysFromNow)
    {
        CountdownDraft draft = new("Launch", Now.AddDays(daysFromNow), tileEnabled: false, toastEnabled: false);

        Assert.True(draft.CanCommit(Now));
    }

    [Fact]
    public void TryCommit_ReturnsStateWithDraftValues()
    {
        DateTimeOffset targetDate = Now.AddDays(5);
        CountdownDraft draft = new("Launch", targetDate, tileEnabled: true, toastEnabled: false);

        bool committed = draft.TryCommit(Now, out CountdownState? state);

        Assert.True(committed);
        Assert.NotNull(state);
        Assert.Equal("Launch", state.Title);
        Assert.Equal(targetDate, state.TargetDate);
        Assert.True(state.TileEnabled);
        Assert.False(state.ToastEnabled);
    }

    [Fact]
    public void TryCommit_ReturnsNullStateForInvalidDraft()
    {
        CountdownDraft draft = new(" ", Now, tileEnabled: true, toastEnabled: true);

        bool committed = draft.TryCommit(Now, out CountdownState? state);

        Assert.False(committed);
        Assert.Null(state);
    }

    [Fact]
    public void TryApplyTo_UpdatesSelectedCountdownAndPreservesOtherCountdowns()
    {
        CountdownState state = new CountdownState(
            new[]
            {
                new CountdownItem("first", "First", Now.AddDays(1)),
                new CountdownItem("second", "Second", Now.AddDays(2))
            },
            selectedCountdownId: "second",
            tileEnabled: false,
            toastEnabled: false);
        CountdownDraft draft = new("Updated", Now.AddDays(3), tileEnabled: true, toastEnabled: true);

        bool committed = draft.TryApplyTo(state, Now, out CountdownState? updatedState);

        Assert.True(committed);
        Assert.NotNull(updatedState);
        Assert.Equal(2, updatedState.Countdowns.Count);
        Assert.Equal("first", updatedState.Countdowns[0].Id);
        Assert.Equal("First", updatedState.Countdowns[0].Title);
        Assert.Equal("second", updatedState.SelectedCountdownId);
        Assert.Equal("Updated", updatedState.Title);
        Assert.Equal(Now.AddDays(3), updatedState.TargetDate);
        Assert.True(updatedState.TileEnabled);
        Assert.True(updatedState.ToastEnabled);
    }
}
