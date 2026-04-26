using DateCountdown.Core;
using Xunit;

namespace DateCountdown.Tests;

public sealed class CountdownPreferencesTests
{
    [Fact]
    public void Default_UsesNonIntrusiveSettings()
    {
        CountdownPreferences preferences = CountdownPreferences.Default;

        Assert.False(preferences.SortCountdownsByDaysLeft);
        Assert.Equal(CountdownDisplaySize.Default, preferences.DisplaySize);
        Assert.False(preferences.OpenWindowAtStartup);
    }

    [Theory]
    [InlineData("Compact", CountdownDisplaySize.Compact)]
    [InlineData("compact", CountdownDisplaySize.Compact)]
    [InlineData("Large", CountdownDisplaySize.Large)]
    [InlineData("unknown", CountdownDisplaySize.Default)]
    [InlineData(null, CountdownDisplaySize.Default)]
    public void ParseDisplaySize_FallsBackForInvalidValues(string? value, CountdownDisplaySize expected)
    {
        Assert.Equal(expected, CountdownPreferences.ParseDisplaySize(value));
    }

    [Fact]
    public void With_ReplacesOnlySpecifiedValues()
    {
        CountdownPreferences preferences = new(
            sortCountdownsByDaysLeft: true,
            displaySize: CountdownDisplaySize.Large,
            openWindowAtStartup: true);

        CountdownPreferences updated = preferences.With(displaySize: CountdownDisplaySize.Compact);

        Assert.True(updated.SortCountdownsByDaysLeft);
        Assert.Equal(CountdownDisplaySize.Compact, updated.DisplaySize);
        Assert.True(updated.OpenWindowAtStartup);
    }
}
