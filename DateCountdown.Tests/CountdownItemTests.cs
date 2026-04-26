using DateCountdown.Core;
using System;
using Xunit;

namespace DateCountdown.Tests;

public sealed class CountdownItemTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_UsesDefaultIdForMissingIds(string? id)
    {
        CountdownItem item = new(id, "Launch", DateTimeOffset.UnixEpoch);

        Assert.Equal(CountdownItem.DefaultId, item.Id);
    }

    [Fact]
    public void Constructor_NormalizesNullTitle()
    {
        CountdownItem item = new("id", null, DateTimeOffset.UnixEpoch);

        Assert.Equal(string.Empty, item.Title);
    }

    [Fact]
    public void Constructor_DefaultsToastToDisabled()
    {
        CountdownItem item = new("id", "Launch", DateTimeOffset.UnixEpoch);

        Assert.False(item.ToastEnabled);
    }

    [Fact]
    public void With_PreservesIdAndUnspecifiedValues()
    {
        DateTimeOffset targetDate = new(2026, 5, 8, 0, 0, 0, TimeSpan.Zero);
        CountdownItem item = new("id", "Launch", targetDate, toastEnabled: true);

        CountdownItem updated = item.With(title: "Updated");

        Assert.Equal("id", updated.Id);
        Assert.Equal("Updated", updated.Title);
        Assert.Equal(targetDate, updated.TargetDate);
        Assert.True(updated.ToastEnabled);
    }

    [Fact]
    public void With_UpdatesToastFlag()
    {
        CountdownItem item = new("id", "Launch", DateTimeOffset.UnixEpoch, toastEnabled: true);

        CountdownItem updated = item.With(toastEnabled: false);

        Assert.False(updated.ToastEnabled);
    }
}
