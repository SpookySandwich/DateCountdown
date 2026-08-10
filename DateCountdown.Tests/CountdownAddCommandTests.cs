using DateCountdown.Core;
using System;
using System.Linq;
using Xunit;

namespace DateCountdown.Tests;

public sealed class CountdownAddCommandTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 17, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Execute_ValidRequest_AddsAndSelectsCountdown()
    {
        CountdownState state = new("Existing", Now.AddDays(1), tileEnabled: true, toastEnabled: true);
        CountdownAddRequest request = new(" Launch ", Now.AddDays(2), "UTC");

        CountdownAddResult result = CountdownAddCommand.Execute(state, request, Now);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.AddedCountdown);
        Assert.Equal(2, result.State.Countdowns.Count);
        Assert.Equal(result.AddedCountdown.Id, result.State.SelectedCountdownId);
        Assert.Equal("Launch", result.State.Title);
        Assert.Equal("UTC", result.State.TargetTimeZoneId);
        Assert.True(result.State.TileEnabled);
        Assert.True(result.State.AnyToastEnabled);
        Assert.False(result.AddedCountdown.ToastEnabled);
    }

    [Fact]
    public void Execute_IanaTimeZone_PreservesValidIdentifier()
    {
        CountdownAddRequest request = new("Launch", Now.AddDays(1), "Asia/Shanghai");

        CountdownAddResult result = CountdownAddCommand.Execute(CountdownState.CreateEmpty(), request, Now);

        Assert.True(result.IsSuccess);
        Assert.Equal("Asia/Shanghai", result.AddedCountdown!.TargetTimeZoneId);
    }

    [Fact]
    public void Execute_RequestId_UsesRequestIdAsCountdownId()
    {
        const string requestId = "97eabec38c7b43cbb7e121d0d4fbfdca";
        CountdownAddRequest request = new("Launch", Now.AddDays(1), "UTC", requestId);

        CountdownAddResult result = CountdownAddCommand.Execute(CountdownState.CreateEmpty(), request, Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(requestId, result.AddedCountdown!.Id);
    }

    [Fact]
    public void Execute_RepeatedRequestId_ReturnsExistingCountdownWithoutDuplicate()
    {
        const string requestId = "97eabec38c7b43cbb7e121d0d4fbfdca";
        CountdownAddRequest request = new("Launch", Now.AddDays(1), "UTC", requestId);
        CountdownAddResult firstResult = CountdownAddCommand.Execute(CountdownState.CreateEmpty(), request, Now);

        CountdownAddResult retryResult = CountdownAddCommand.Execute(firstResult.State, request, Now);

        Assert.True(retryResult.IsSuccess);
        Assert.Same(firstResult.State, retryResult.State);
        Assert.Same(firstResult.AddedCountdown, retryResult.AddedCountdown);
        Assert.Single(retryResult.State.Countdowns);
    }

    [Fact]
    public void Execute_RepeatedRequestIdAfterTargetPasses_ReturnsExistingCountdown()
    {
        const string requestId = "97eabec38c7b43cbb7e121d0d4fbfdca";
        CountdownAddRequest request = new("Launch", Now.AddSeconds(1), "UTC", requestId);
        CountdownAddResult firstResult = CountdownAddCommand.Execute(CountdownState.CreateEmpty(), request, Now);

        CountdownAddResult retryResult = CountdownAddCommand.Execute(firstResult.State, request, Now.AddSeconds(2));

        Assert.True(retryResult.IsSuccess);
        Assert.Same(firstResult.AddedCountdown, retryResult.AddedCountdown);
        Assert.Single(retryResult.State.Countdowns);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Execute_BlankTitle_ReturnsTitleRequired(string? title)
    {
        CountdownAddRequest request = new(title, Now.AddDays(1), "UTC");

        CountdownAddResult result = CountdownAddCommand.Execute(CountdownState.CreateEmpty(), request, Now);

        Assert.False(result.IsSuccess);
        Assert.Equal(CountdownAddError.TitleRequired, result.Error);
        Assert.Empty(result.State.Countdowns);
    }

    [Fact]
    public void Execute_TitleOverTextElementLimit_ReturnsTitleTooLong()
    {
        string title = string.Concat(Enumerable.Repeat("😀", CountdownItem.MaxTitleLength + 1));
        CountdownAddRequest request = new(title, Now.AddDays(1), "UTC");

        CountdownAddResult result = CountdownAddCommand.Execute(CountdownState.CreateEmpty(), request, Now);

        Assert.False(result.IsSuccess);
        Assert.Equal(CountdownAddError.TitleTooLong, result.Error);
    }

    [Fact]
    public void Execute_PastTarget_ReturnsTargetDateInPast()
    {
        CountdownAddRequest request = new("Launch", Now.AddTicks(-1), "UTC");

        CountdownAddResult result = CountdownAddCommand.Execute(CountdownState.CreateEmpty(), request, Now);

        Assert.False(result.IsSuccess);
        Assert.Equal(CountdownAddError.TargetDateInPast, result.Error);
    }

    [Fact]
    public void Execute_TargetAtCurrentInstant_AddsCountdown()
    {
        CountdownAddRequest request = new("Launch", Now, "UTC");

        CountdownAddResult result = CountdownAddCommand.Execute(CountdownState.CreateEmpty(), request, Now);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Execute_InvalidTimeZone_ReturnsInvalidTimeZone()
    {
        CountdownAddRequest request = new("Launch", Now.AddDays(1), "Not/A-Time-Zone");

        CountdownAddResult result = CountdownAddCommand.Execute(CountdownState.CreateEmpty(), request, Now);

        Assert.False(result.IsSuccess);
        Assert.Equal(CountdownAddError.InvalidTimeZone, result.Error);
    }

    [Fact]
    public void Execute_InvalidRequestId_ReturnsInvalidRequestId()
    {
        CountdownAddRequest request = new("Launch", Now.AddDays(1), "UTC", "not-a-request-id");

        CountdownAddResult result = CountdownAddCommand.Execute(CountdownState.CreateEmpty(), request, Now);

        Assert.False(result.IsSuccess);
        Assert.Equal(CountdownAddError.InvalidRequestId, result.Error);
    }
}
