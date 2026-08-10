using DateCountdown.Core;
using System;
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
        CountdownDisplayText displayText = new(new CountdownDisplayTextResources
        {
            DefaultTitle = defaultTitle,
            OneDayLeftFormat = oneDayFormat,
            ManyDaysLeftFormat = manyDaysFormat
        });

        Assert.Equal("Date Countdown", displayText.DefaultTitle);
        Assert.Equal("Due today", displayText.FormatDaysLeft(0, CultureInfo.InvariantCulture));
        Assert.Equal("1 day left", displayText.FormatDaysLeft(1, CultureInfo.InvariantCulture));
        Assert.Equal("2 days left", displayText.FormatDaysLeft(2, CultureInfo.InvariantCulture));
        Assert.Equal("1 hour left", displayText.FormatHoursLeft(1, CultureInfo.InvariantCulture));
        Assert.Equal("2 hours left", displayText.FormatHoursLeft(2, CultureInfo.InvariantCulture));
        Assert.Equal("1 day 1 hour left", displayText.FormatDaysAndHoursLeft(25, CultureInfo.InvariantCulture));
        Assert.Equal("1 day 2 hours left", displayText.FormatDaysAndHoursLeft(26, CultureInfo.InvariantCulture));
        Assert.Equal("2 days 1 hour left", displayText.FormatDaysAndHoursLeft(49, CultureInfo.InvariantCulture));
        Assert.Equal("2 days 2 hours left", displayText.FormatDaysAndHoursLeft(50, CultureInfo.InvariantCulture));
        Assert.Equal("1 day overdue", displayText.FormatDaysLeft(-1, CultureInfo.InvariantCulture));
        Assert.Equal("2 days overdue", displayText.FormatDaysLeft(-2, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void FormatDaysLeft_UsesDueTodayAndOverdueForNonFutureDates()
    {
        CountdownDisplayText displayText = new(new CountdownDisplayTextResources
        {
            DefaultTitle = "Countdown",
            OneDayLeftFormat = "single {0}",
            ManyDaysLeftFormat = "many {0}",
            DueTodayText = "today",
            OneDayOverdueFormat = "overdue single {0}",
            ManyDaysOverdueFormat = "overdue many {0}"
        });

        Assert.Equal("today", displayText.FormatDaysLeft(0, CultureInfo.InvariantCulture));
        Assert.Equal("single 1", displayText.FormatDaysLeft(1, CultureInfo.InvariantCulture));
        Assert.Equal("many 2", displayText.FormatDaysLeft(2, CultureInfo.InvariantCulture));
        Assert.Equal("overdue single 1", displayText.FormatDaysLeft(-1, CultureInfo.InvariantCulture));
        Assert.Equal("overdue many 2", displayText.FormatDaysLeft(-2, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void FormatDaysLeft_UsesProvidedCulture()
    {
        CountdownDisplayText displayText = CreateDisplayText("{0:N0}", "{0:N0}");

        Assert.Equal("1,234", displayText.FormatDaysLeft(1234, CultureInfo.GetCultureInfo("en-US")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FormatTitle_UsesDefaultForMissingTitle(string? title)
    {
        CountdownDisplayText displayText = CreateDisplayText();

        Assert.Equal("Countdown", displayText.FormatTitle(title));
    }

    [Fact]
    public void FormatTitle_NormalizesProvidedTitle()
    {
        CountdownDisplayText displayText = CreateDisplayText();

        Assert.Equal("Launch", displayText.FormatTitle("  Launch  "));
    }

    [Fact]
    public void FormatDaysLeft_UsesFallbackWhenLocalizedFormatIsMalformed()
    {
        CountdownDisplayText displayText = CreateDisplayText("{0 day", "{0 days");

        Assert.Equal("2 days left", displayText.FormatDaysLeft(2, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void FormatCountdown_UsesHoursWithinConfiguredThreshold()
    {
        CountdownDisplayText displayText = CreateDisplayText();
        CountdownPreferences preferences = new(showHoursWithinThreshold: true, hoursDisplayThresholdDays: 2);
        DateTimeOffset now = new(2026, 4, 25, 10, 30, 0, TimeSpan.Zero);

        Assert.Equal("38 hours left", displayText.FormatCountdown(now.AddDays(1).AddHours(14), now, preferences, CultureInfo.InvariantCulture));
        Assert.Equal("3 days", displayText.FormatCountdown(now.AddDays(3), now, preferences, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void FormatCountdown_DropsZeroHourRemainder()
    {
        CountdownDisplayText displayText = CreateDisplayText();
        CountdownPreferences preferences = new(showHoursWithinThreshold: true, hoursDisplayThresholdDays: 2);
        DateTimeOffset now = new(2026, 4, 25, 0, 0, 0, TimeSpan.Zero);

        Assert.Equal("24 hours left", displayText.FormatCountdown(now.AddDays(1), now, preferences, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void FormatCountdown_DoesNotUseHoursWhenDisabled()
    {
        CountdownDisplayText displayText = new(new CountdownDisplayTextResources
        {
            DefaultTitle = "Countdown",
            OneDayLeftFormat = "{0} day",
            ManyDaysLeftFormat = "{0} days",
            DueTodayText = "today",
            OneHourLeftFormat = "{0} hour",
            ManyHoursLeftFormat = "{0} hours"
        });
        DateTimeOffset now = new(2026, 4, 25, 10, 30, 0, TimeSpan.Zero);

        CountdownPreferences preferences = new(showHoursWithinThreshold: false);

        Assert.Equal("1 day", displayText.FormatCountdown(now.AddDays(1), now, preferences, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void FormatCountdown_UsesHoursOnlyMode()
    {
        CountdownDisplayText displayText = CreateDisplayText();
        CountdownPreferences preferences = new(hourDisplayMode: CountdownHourDisplayMode.HoursOnly);
        DateTimeOffset now = new(2026, 4, 25, 10, 30, 0, TimeSpan.Zero);

        Assert.Equal("158 hours left", displayText.FormatCountdown(now.AddDays(6).AddHours(14), now, preferences, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void FormatCountdown_UsesDaysAndHoursMode()
    {
        CountdownDisplayText displayText = CreateDisplayText();
        CountdownPreferences preferences = new(hourDisplayMode: CountdownHourDisplayMode.DaysAndHours);
        DateTimeOffset now = new(2026, 4, 25, 10, 30, 0, TimeSpan.Zero);

        Assert.Equal("6 days 14 hours left", displayText.FormatCountdown(now.AddDays(6).AddHours(14), now, preferences, CultureInfo.InvariantCulture));
        Assert.Equal("1 day", displayText.FormatCountdown(now.AddDays(1), now, preferences, CultureInfo.InvariantCulture));
        Assert.Equal("2 hours left", displayText.FormatCountdown(now.AddHours(2), now, preferences, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void FormatCountdownContent_UsesSeparateVisualPartsForDaysAndHoursMode()
    {
        CountdownDisplayText displayText = CreateDisplayText();
        CountdownPreferences preferences = new(hourDisplayMode: CountdownHourDisplayMode.DaysAndHours);
        DateTimeOffset now = new(2026, 4, 25, 10, 30, 0, TimeSpan.Zero);

        CountdownDisplayContent content = displayText.FormatCountdownContent(
            now.AddDays(9).AddHours(21),
            now,
            preferences,
            CultureInfo.InvariantCulture);

        Assert.Equal("9 days", content.PrimaryText);
        Assert.Equal("21 hours", content.SecondaryText);
        Assert.Equal("9 days 21 hours left", content.AccessibleText);
        Assert.Equal(CountdownDisplayLayoutKind.DaysAndHours, content.LayoutKind);
        Assert.True(content.HasSecondaryText);
    }

    [Fact]
    public void FormatCountdownContent_KeepsSingleLineForOtherModes()
    {
        CountdownDisplayText displayText = CreateDisplayText();
        CountdownPreferences preferences = new(hourDisplayMode: CountdownHourDisplayMode.HoursOnly);
        DateTimeOffset now = new(2026, 4, 25, 10, 30, 0, TimeSpan.Zero);

        CountdownDisplayContent content = displayText.FormatCountdownContent(
            now.AddDays(9).AddHours(21),
            now,
            preferences,
            CultureInfo.InvariantCulture);

        Assert.Equal("237 hours left", content.PrimaryText);
        Assert.Equal(string.Empty, content.SecondaryText);
        Assert.Equal("237 hours left", content.AccessibleText);
        Assert.Equal(CountdownDisplayLayoutKind.SingleLine, content.LayoutKind);
        Assert.False(content.HasSecondaryText);
    }

    private static CountdownDisplayText CreateDisplayText(
        string oneDayLeftFormat = "{0} day",
        string manyDaysLeftFormat = "{0} days")
    {
        return new CountdownDisplayText(new CountdownDisplayTextResources
        {
            DefaultTitle = "Countdown",
            OneDayLeftFormat = oneDayLeftFormat,
            ManyDaysLeftFormat = manyDaysLeftFormat
        });
    }
}
