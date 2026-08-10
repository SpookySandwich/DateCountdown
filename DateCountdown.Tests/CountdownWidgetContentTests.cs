using DateCountdown.Core;
using System;
using System.Text.Json;
using Xunit;

namespace DateCountdown.Tests;

public sealed class CountdownWidgetContentTests
{
    [Fact]
    public void BuildTemplateJson_ProducesValidAdaptiveCardTemplate()
    {
        using JsonDocument document = JsonDocument.Parse(CountdownWidgetContent.BuildTemplateJson());
        JsonElement root = document.RootElement;

        Assert.Equal("AdaptiveCard", root.GetProperty("type").GetString());
        Assert.Equal("1.5", root.GetProperty("version").GetString());
        Assert.Equal("Center", root.GetProperty("verticalContentAlignment").GetString());

        JsonElement body = root.GetProperty("body");
        Assert.Single(body.EnumerateArray());
        Assert.Equal("Container", body[0].GetProperty("type").GetString());
        Assert.Equal("126px", body[0].GetProperty("minHeight").GetString());
        Assert.Equal("Center", body[0].GetProperty("verticalContentAlignment").GetString());

        JsonElement columns = body[0].GetProperty("items")[0].GetProperty("columns");
        Assert.Equal("stretch", columns[0].GetProperty("width").GetString());
        Assert.Equal("224px", columns[1].GetProperty("width").GetString());
        Assert.Equal("stretch", columns[2].GetProperty("width").GetString());

        JsonElement contentPanel = columns[1].GetProperty("items")[0];
        Assert.False(contentPanel.TryGetProperty("style", out _));

        JsonElement contentItems = contentPanel.GetProperty("items");
        Assert.Equal("${daysText}", contentItems[0].GetProperty("text").GetString());
        Assert.Equal("Center", contentItems[0].GetProperty("horizontalAlignment").GetString());
        Assert.Equal("${title}", contentItems[1].GetProperty("text").GetString());
        Assert.Equal("Center", contentItems[1].GetProperty("horizontalAlignment").GetString());
        Assert.Equal("${targetDateText}", contentItems[2].GetProperty("text").GetString());
        Assert.Equal("Center", contentItems[2].GetProperty("horizontalAlignment").GetString());
    }

    [Fact]
    public void BuildTemplateJson_ForMultipleCountdowns_AddsCompactListRows()
    {
        CountdownState state = new CountdownState(
            new[]
            {
                new CountdownItem("first", "First", DateTimeOffset.UnixEpoch),
                new CountdownItem("second", "Second", DateTimeOffset.UnixEpoch.AddDays(1)),
                new CountdownItem("third", "Third", DateTimeOffset.UnixEpoch.AddDays(2))
            },
            selectedCountdownId: "first",
            tileEnabled: false,
            toastEnabled: false);

        using JsonDocument document = JsonDocument.Parse(CountdownWidgetContent.BuildTemplateJson(state));

        JsonElement contentItems = GetWidgetContentItems(document);
        Assert.Equal(4, contentItems.GetArrayLength());
        JsonElement listContainer = contentItems[3];
        Assert.Equal("Container", listContainer.GetProperty("type").GetString());
        Assert.True(listContainer.GetProperty("separator").GetBoolean());
        Assert.Equal(2, listContainer.GetProperty("items").GetArrayLength());
        Assert.Equal("${item0Title}", listContainer.GetProperty("items")[0].GetProperty("columns")[0].GetProperty("items")[0].GetProperty("text").GetString());
        Assert.Equal("${item0DaysText}", listContainer.GetProperty("items")[0].GetProperty("columns")[1].GetProperty("items")[0].GetProperty("text").GetString());
    }

    [Fact]
    public void BuildTemplateJson_ForSmallWidget_OmitsListRows()
    {
        CountdownState state = new CountdownState(
            new[]
            {
                new CountdownItem("first", "First", DateTimeOffset.UnixEpoch),
                new CountdownItem("second", "Second", DateTimeOffset.UnixEpoch.AddDays(1))
            },
            selectedCountdownId: "first",
            tileEnabled: false,
            toastEnabled: false);

        using JsonDocument document = JsonDocument.Parse(
            CountdownWidgetContent.BuildTemplateJson(state, CountdownWidgetSize.Small));

        Assert.Equal(3, GetWidgetContentItems(document).GetArrayLength());
    }

    [Fact]
    public void BuildTemplateJson_ForMediumWidget_LimitsListRows()
    {
        CountdownState state = new CountdownState(
            new[]
            {
                new CountdownItem("selected", "Selected", DateTimeOffset.UnixEpoch),
                new CountdownItem("one", "One", DateTimeOffset.UnixEpoch.AddDays(1)),
                new CountdownItem("two", "Two", DateTimeOffset.UnixEpoch.AddDays(2)),
                new CountdownItem("three", "Three", DateTimeOffset.UnixEpoch.AddDays(3)),
                new CountdownItem("four", "Four", DateTimeOffset.UnixEpoch.AddDays(4)),
                new CountdownItem("five", "Five", DateTimeOffset.UnixEpoch.AddDays(5)),
                new CountdownItem("six", "Six", DateTimeOffset.UnixEpoch.AddDays(6)),
                new CountdownItem("seven", "Seven", DateTimeOffset.UnixEpoch.AddDays(7)),
                new CountdownItem("eight", "Eight", DateTimeOffset.UnixEpoch.AddDays(8))
            },
            selectedCountdownId: "selected",
            tileEnabled: false,
            toastEnabled: false);

        using JsonDocument document = JsonDocument.Parse(
            CountdownWidgetContent.BuildTemplateJson(state, CountdownWidgetSize.Medium));

        JsonElement contentItems = GetWidgetContentItems(document);
        Assert.Equal(4, contentItems.GetArrayLength());
        Assert.Equal(4, contentItems[3].GetProperty("items").GetArrayLength());
    }

    [Fact]
    public void BuildTemplateJson_ForLargeWidget_LimitsListRows()
    {
        CountdownState state = new CountdownState(
            new[]
            {
                new CountdownItem("selected", "Selected", DateTimeOffset.UnixEpoch),
                new CountdownItem("one", "One", DateTimeOffset.UnixEpoch.AddDays(1)),
                new CountdownItem("two", "Two", DateTimeOffset.UnixEpoch.AddDays(2)),
                new CountdownItem("three", "Three", DateTimeOffset.UnixEpoch.AddDays(3)),
                new CountdownItem("four", "Four", DateTimeOffset.UnixEpoch.AddDays(4)),
                new CountdownItem("five", "Five", DateTimeOffset.UnixEpoch.AddDays(5)),
                new CountdownItem("six", "Six", DateTimeOffset.UnixEpoch.AddDays(6)),
                new CountdownItem("seven", "Seven", DateTimeOffset.UnixEpoch.AddDays(7)),
                new CountdownItem("eight", "Eight", DateTimeOffset.UnixEpoch.AddDays(8)),
                new CountdownItem("nine", "Nine", DateTimeOffset.UnixEpoch.AddDays(9))
            },
            selectedCountdownId: "selected",
            tileEnabled: false,
            toastEnabled: false);

        using JsonDocument document = JsonDocument.Parse(
            CountdownWidgetContent.BuildTemplateJson(state, CountdownWidgetSize.Large));

        JsonElement contentItems = GetWidgetContentItems(document);
        Assert.Equal(4, contentItems.GetArrayLength());
        Assert.Equal(7, contentItems[3].GetProperty("items").GetArrayLength());
    }

    [Fact]
    public void BuildDataJson_UsesLocalizedDaysTextAndTitle()
    {
        using CultureScope scope = new("en-US");
        CountdownDisplayText displayText = CreateDisplayText();
        DateTimeOffset now = new(2026, 4, 25, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset targetDate = new(2026, 5, 8, 0, 0, 0, TimeSpan.Zero);

        CountdownState state = new CountdownState(
            new[] { new CountdownItem("selected", "Launch", targetDate, targetTimeZoneId: "UTC") },
            selectedCountdownId: "selected",
            tileEnabled: false,
            toastEnabled: false);

        using JsonDocument document = JsonDocument.Parse(
            CountdownWidgetContent.BuildDataJson(
                state,
                now,
                displayText,
                preferences: new CountdownPreferences(showHoursWithinThreshold: false)));

        JsonElement root = document.RootElement;
        Assert.Equal("13 days", root.GetProperty("daysText").GetString());
        Assert.Equal("Launch", root.GetProperty("title").GetString());
        Assert.Equal("5/8/2026 12:00 AM (UTC)", root.GetProperty("targetDateText").GetString());
    }

    [Fact]
    public void BuildDataJson_UsesLocalizedDefaultTitleForBlankTitle()
    {
        CountdownDisplayText displayText = new(new CountdownDisplayTextResources
        {
            DefaultTitle = "日期倒计时",
            OneDayLeftFormat = "剩余{0}天",
            ManyDaysLeftFormat = "剩余{0}天",
            DueTodayText = "今天到期"
        });
        DateTimeOffset now = new(2026, 4, 25, 0, 0, 0, TimeSpan.Zero);

        using JsonDocument document = JsonDocument.Parse(
            CountdownWidgetContent.BuildDataJson("   ", now, now, displayText));

        Assert.Equal("日期倒计时", document.RootElement.GetProperty("title").GetString());
        Assert.Equal("今天到期", document.RootElement.GetProperty("daysText").GetString());
    }

    [Fact]
    public void BuildDataJson_WithEmptyStateReturnsNoCountdownData()
    {
        CountdownDisplayText displayText = CreateDisplayText("Date Countdown");
        DateTimeOffset now = new(2026, 4, 25, 0, 0, 0, TimeSpan.Zero);

        using JsonDocument document = JsonDocument.Parse(
            CountdownWidgetContent.BuildDataJson(CountdownState.CreateEmpty(), now, displayText));

        JsonElement root = document.RootElement;
        Assert.Equal(string.Empty, root.GetProperty("daysText").GetString());
        Assert.Equal("Date Countdown", root.GetProperty("title").GetString());
        Assert.Equal(string.Empty, root.GetProperty("targetDateText").GetString());
        Assert.False(root.TryGetProperty("item0Title", out _));
    }

    [Fact]
    public void BuildDataJson_UsesHoursWhenPreferenceIsEnabled()
    {
        CountdownDisplayText displayText = CreateDisplayText();
        CountdownPreferences preferences = new(showHoursWithinThreshold: true, hoursDisplayThresholdDays: 2);
        DateTimeOffset now = new(2026, 4, 25, 12, 30, 0, TimeSpan.Zero);

        using JsonDocument document = JsonDocument.Parse(
            CountdownWidgetContent.BuildDataJson("Launch", now.AddDays(1).AddHours(12), now, displayText, preferences));

        Assert.Equal("36 hours left", document.RootElement.GetProperty("daysText").GetString());
    }

    [Fact]
    public void BuildDataJson_UsesDaysAndHoursWhenPreferenceIsEnabled()
    {
        CountdownDisplayText displayText = CreateDisplayText();
        CountdownPreferences preferences = new(hourDisplayMode: CountdownHourDisplayMode.DaysAndHours);
        DateTimeOffset now = new(2026, 4, 25, 12, 30, 0, TimeSpan.Zero);

        using JsonDocument document = JsonDocument.Parse(
            CountdownWidgetContent.BuildDataJson("Launch", now.AddDays(1).AddHours(12), now, displayText, preferences));

        Assert.Equal("1 day 12 hours left", document.RootElement.GetProperty("daysText").GetString());
    }

    [Fact]
    public void BuildDataJson_ForMultipleCountdowns_IncludesUpcomingListItems()
    {
        using CultureScope scope = new("en-US");
        CountdownDisplayText displayText = CreateDisplayText();
        DateTimeOffset now = new(2026, 4, 25, 0, 0, 0, TimeSpan.Zero);
        CountdownState state = new CountdownState(
            new[]
            {
                new CountdownItem("selected", "Selected", now.AddDays(10)),
                new CountdownItem("later", "Later", now.AddDays(30)),
                new CountdownItem("soon", "Soon", now.AddDays(2))
            },
            selectedCountdownId: "selected",
            tileEnabled: false,
            toastEnabled: false);

        using JsonDocument document = JsonDocument.Parse(
            CountdownWidgetContent.BuildDataJson(
                state,
                now,
                displayText,
                preferences: new CountdownPreferences(showHoursWithinThreshold: false)));

        JsonElement root = document.RootElement;
        Assert.Equal("10 days", root.GetProperty("daysText").GetString());
        Assert.Equal("Selected", root.GetProperty("title").GetString());
        Assert.Equal("Soon", root.GetProperty("item0Title").GetString());
        Assert.Equal("2 days", root.GetProperty("item0DaysText").GetString());
        Assert.Equal("Later", root.GetProperty("item1Title").GetString());
        Assert.Equal("30 days", root.GetProperty("item1DaysText").GetString());
    }

    [Fact]
    public void BuildDataJson_ForMultipleCountdowns_LimitsListItems()
    {
        CountdownDisplayText displayText = CreateDisplayText();
        DateTimeOffset now = new(2026, 4, 25, 0, 0, 0, TimeSpan.Zero);
        CountdownState state = new CountdownState(
            new[]
            {
                new CountdownItem("selected", "Selected", now),
                new CountdownItem("one", "One", now.AddDays(1)),
                new CountdownItem("two", "Two", now.AddDays(2)),
                new CountdownItem("three", "Three", now.AddDays(3)),
                new CountdownItem("four", "Four", now.AddDays(4)),
                new CountdownItem("five", "Five", now.AddDays(5)),
                new CountdownItem("six", "Six", now.AddDays(6)),
                new CountdownItem("seven", "Seven", now.AddDays(7)),
                new CountdownItem("eight", "Eight", now.AddDays(8))
            },
            selectedCountdownId: "selected",
            tileEnabled: false,
            toastEnabled: false);

        using JsonDocument document = JsonDocument.Parse(
            CountdownWidgetContent.BuildDataJson(state, now, displayText));

        Assert.True(document.RootElement.TryGetProperty("item6Title", out _));
        Assert.False(document.RootElement.TryGetProperty("item7Title", out _));
    }

    [Fact]
    public void BuildDataJson_ForSmallWidget_OmitsListItems()
    {
        CountdownDisplayText displayText = CreateDisplayText();
        DateTimeOffset now = new(2026, 4, 25, 0, 0, 0, TimeSpan.Zero);
        CountdownState state = new CountdownState(
            new[]
            {
                new CountdownItem("selected", "Selected", now),
                new CountdownItem("one", "One", now.AddDays(1))
            },
            selectedCountdownId: "selected",
            tileEnabled: false,
            toastEnabled: false);

        using JsonDocument document = JsonDocument.Parse(
            CountdownWidgetContent.BuildDataJson(state, now, displayText, CountdownWidgetSize.Small));

        Assert.False(document.RootElement.TryGetProperty("item0Title", out _));
        Assert.False(document.RootElement.TryGetProperty("item0DaysText", out _));
    }

    [Fact]
    public void BuildDataJson_EscapesJsonSpecialCharacters()
    {
        CountdownDisplayText displayText = CreateDisplayText();
        DateTimeOffset now = new(2026, 4, 25, 0, 0, 0, TimeSpan.Zero);
        string title = "Quote \" slash \\ newline\n control " + '\u0001';

        using JsonDocument document = JsonDocument.Parse(
            CountdownWidgetContent.BuildDataJson(title, now, now, displayText));

        Assert.Equal(title, document.RootElement.GetProperty("title").GetString());
    }

    private static CountdownDisplayText CreateDisplayText(string defaultTitle = "Countdown")
    {
        return new CountdownDisplayText(new CountdownDisplayTextResources
        {
            DefaultTitle = defaultTitle,
            OneDayLeftFormat = "{0} day",
            ManyDaysLeftFormat = "{0} days"
        });
    }

    private static JsonElement GetWidgetContentItems(JsonDocument document)
    {
        return document.RootElement
            .GetProperty("body")[0]
            .GetProperty("items")[0]
            .GetProperty("columns")[1]
            .GetProperty("items")[0]
            .GetProperty("items");
    }
}
