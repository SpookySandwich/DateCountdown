using DateCountdown.Core;
using System.Globalization;
using Xunit;

namespace DateCountdown.Tests;

public sealed class CountdownDisplayTextTests
{
    [Theory]
    [InlineData(null, null, null)]
    [InlineData("", "", "")]
    [InlineData("   ", "   ", "   ")]
    public void Constructor_UsesFallbacksForMissingFormats(string? defaultTitle, string? oneDayFormat, string? manyDaysFormat)
    {
        CountdownDisplayText displayText = new(defaultTitle, oneDayFormat, manyDaysFormat);

        Assert.Equal("Date Countdown", displayText.DefaultTitle);
        Assert.Equal("1 day left", displayText.FormatDaysLeft(1, CultureInfo.InvariantCulture));
        Assert.Equal("2 days left", displayText.FormatDaysLeft(2, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void FormatDaysLeft_UsesSingularFormatOnlyForOne()
    {
        CountdownDisplayText displayText = new("Countdown", "single {0}", "many {0}");

        Assert.Equal("many 0", displayText.FormatDaysLeft(0, CultureInfo.InvariantCulture));
        Assert.Equal("single 1", displayText.FormatDaysLeft(1, CultureInfo.InvariantCulture));
        Assert.Equal("many 2", displayText.FormatDaysLeft(2, CultureInfo.InvariantCulture));
        Assert.Equal("many -1", displayText.FormatDaysLeft(-1, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void FormatDaysLeft_UsesProvidedCulture()
    {
        CountdownDisplayText displayText = new("Countdown", "{0:N0}", "{0:N0}");

        Assert.Equal("1,234", displayText.FormatDaysLeft(1234, CultureInfo.GetCultureInfo("en-US")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FormatTitle_UsesDefaultForMissingTitle(string? title)
    {
        CountdownDisplayText displayText = new("Countdown", "{0} day", "{0} days");

        Assert.Equal("Countdown", displayText.FormatTitle(title));
    }

    [Fact]
    public void FormatTitle_NormalizesProvidedTitle()
    {
        CountdownDisplayText displayText = new("Countdown", "{0} day", "{0} days");

        Assert.Equal("Launch", displayText.FormatTitle("  Launch  "));
    }

    [Fact]
    public void FormatDaysLeft_UsesFallbackWhenLocalizedFormatIsMalformed()
    {
        CountdownDisplayText displayText = new("Countdown", "{0 day", "{0 days");

        Assert.Equal("2 days left", displayText.FormatDaysLeft(2, CultureInfo.InvariantCulture));
    }
}
