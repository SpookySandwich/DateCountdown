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
        Assert.Equal("${daysText}", root.GetProperty("body")[0].GetProperty("text").GetString());
        Assert.Equal("${title}", root.GetProperty("body")[1].GetProperty("text").GetString());
        Assert.Equal("${targetDateText}", root.GetProperty("body")[2].GetProperty("text").GetString());
    }

    [Fact]
    public void BuildDataJson_UsesLocalizedDaysTextAndTitle()
    {
        using CultureScope scope = new("en-US");
        CountdownDisplayText displayText = new("Countdown", "{0} day", "{0} days");
        DateTimeOffset now = new(2026, 4, 25, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset targetDate = new(2026, 5, 8, 0, 0, 0, TimeSpan.Zero);

        using JsonDocument document = JsonDocument.Parse(
            CountdownWidgetContent.BuildDataJson("Launch", targetDate, now, displayText));

        JsonElement root = document.RootElement;
        Assert.Equal("13 days", root.GetProperty("daysText").GetString());
        Assert.Equal("Launch", root.GetProperty("title").GetString());
        Assert.Equal("5/8/2026", root.GetProperty("targetDateText").GetString());
    }

    [Fact]
    public void BuildDataJson_UsesLocalizedDefaultTitleForBlankTitle()
    {
        CountdownDisplayText displayText = new("日期倒计时", "剩余{0}天", "剩余{0}天");
        DateTimeOffset now = new(2026, 4, 25, 0, 0, 0, TimeSpan.Zero);

        using JsonDocument document = JsonDocument.Parse(
            CountdownWidgetContent.BuildDataJson("   ", now, now, displayText));

        Assert.Equal("日期倒计时", document.RootElement.GetProperty("title").GetString());
        Assert.Equal("剩余0天", document.RootElement.GetProperty("daysText").GetString());
    }

    [Fact]
    public void BuildDataJson_EscapesJsonSpecialCharacters()
    {
        CountdownDisplayText displayText = new("Countdown", "{0} day", "{0} days");
        DateTimeOffset now = new(2026, 4, 25, 0, 0, 0, TimeSpan.Zero);
        string title = "Quote \" slash \\ newline\n control " + '\u0001';

        using JsonDocument document = JsonDocument.Parse(
            CountdownWidgetContent.BuildDataJson(title, now, now, displayText));

        Assert.Equal(title, document.RootElement.GetProperty("title").GetString());
    }
}
