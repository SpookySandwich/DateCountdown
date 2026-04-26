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
}
