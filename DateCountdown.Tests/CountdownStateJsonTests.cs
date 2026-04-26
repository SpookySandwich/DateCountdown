using DateCountdown.Core;
using System;
using System.Linq;
using Xunit;

namespace DateCountdown.Tests;

public sealed class CountdownStateJsonTests
{
    [Fact]
    public void SerializeAndDeserializeCountdowns_RoundTripsItems()
    {
        CountdownItem[] countdowns =
        {
            new("first", "Launch", new DateTimeOffset(2026, 5, 8, 0, 0, 0, TimeSpan.Zero), toastEnabled: true),
            new("second", "Trip", new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero), toastEnabled: false)
        };

        string json = CountdownStateJson.SerializeCountdowns(countdowns);
        var result = CountdownStateJson.DeserializeCountdowns(json).ToList();

        Assert.Equal(2, result.Count);
        Assert.Equal("first", result[0].Id);
        Assert.Equal("Launch", result[0].Title);
        Assert.Equal(countdowns[0].TargetDate, result[0].TargetDate);
        Assert.True(result[0].ToastEnabled);
        Assert.Equal("second", result[1].Id);
        Assert.Equal("Trip", result[1].Title);
        Assert.Equal(countdowns[1].TargetDate, result[1].TargetDate);
        Assert.False(result[1].ToastEnabled);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{}")]
    public void DeserializeCountdowns_ReturnsEmptyForMissingOrInvalidJson(string? value)
    {
        Assert.Empty(CountdownStateJson.DeserializeCountdowns(value));
    }

    [Fact]
    public void DeserializeCountdowns_SkipsItemsWithoutIds()
    {
        string json = """
            [
              { "id": "", "title": "No id", "targetDate": "2026-05-08T00:00:00+00:00" },
              { "id": "valid", "title": "Valid", "targetDate": "2026-06-01T00:00:00+00:00" }
            ]
            """;

        var result = CountdownStateJson.DeserializeCountdowns(json).ToList();

        Assert.Single(result);
        Assert.Equal("valid", result[0].Id);
    }

    [Fact]
    public void DeserializeCountdowns_DefaultsMissingToastFlagToDisabled()
    {
        string json = """
            [
              { "id": "legacy", "title": "Legacy", "targetDate": "2026-05-08T00:00:00+00:00" }
            ]
            """;

        var result = CountdownStateJson.DeserializeCountdowns(json).ToList();

        Assert.Single(result);
        Assert.False(result[0].ToastEnabled);
    }
}
