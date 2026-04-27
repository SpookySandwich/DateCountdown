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
        Assert.Equal(CountdownPreferences.DefaultDaysTextSize, preferences.DaysTextSize);
        Assert.Equal(CountdownPreferences.DefaultTitleTextSize, preferences.TitleTextSize);
        Assert.False(preferences.OpenWindowAtStartup);
    }

    [Theory]
    [InlineData(1, CountdownPreferences.MinDaysTextSize)]
    [InlineData(76.4, 76)]
    [InlineData(999, CountdownPreferences.MaxDaysTextSize)]
    public void CoerceDaysTextSize_ConstrainsValues(double value, double expected)
    {
        Assert.Equal(expected, CountdownPreferences.CoerceDaysTextSize(value));
    }

    [Theory]
    [InlineData(1, CountdownPreferences.MinTitleTextSize)]
    [InlineData(23.5, 24)]
    [InlineData(999, CountdownPreferences.MaxTitleTextSize)]
    public void CoerceTitleTextSize_ConstrainsValues(double value, double expected)
    {
        Assert.Equal(expected, CountdownPreferences.CoerceTitleTextSize(value));
    }

    [Fact]
    public void GetLegacyTextSizes_MapsPreviousDisplaySizeValues()
    {
        (double daysTextSize, double titleTextSize) = CountdownPreferences.GetLegacyTextSizes(CountdownDisplaySize.Large);

        Assert.Equal(84, daysTextSize);
        Assert.Equal(16, titleTextSize);
    }

    [Fact]
    public void With_ReplacesOnlySpecifiedValues()
    {
        CountdownPreferences preferences = new(
            sortCountdownsByDaysLeft: true,
            daysTextSize: 84,
            titleTextSize: 16,
            openWindowAtStartup: true);

        CountdownPreferences updated = preferences.With(daysTextSize: 60);

        Assert.True(updated.SortCountdownsByDaysLeft);
        Assert.Equal(60, updated.DaysTextSize);
        Assert.Equal(16, updated.TitleTextSize);
        Assert.True(updated.OpenWindowAtStartup);
    }
}
