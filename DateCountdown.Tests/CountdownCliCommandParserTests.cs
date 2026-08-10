using DateCountdown.Core;
using System;
using Xunit;

namespace DateCountdown.Tests;

public sealed class CountdownCliCommandParserTests
{
    [Fact]
    public void Parse_NoArguments_ReturnsHelpCommand()
    {
        CountdownCliParseResult result = CountdownCliCommandParser.Parse(Array.Empty<string>());

        Assert.True(result.IsSuccess);
        Assert.Equal(CountdownCliCommandKind.Help, result.Command!.Kind);
    }

    [Fact]
    public void Parse_NullArguments_ReturnsHelpCommand()
    {
        CountdownCliParseResult result = CountdownCliCommandParser.Parse(null);

        Assert.True(result.IsSuccess);
        Assert.Equal(CountdownCliCommandKind.Help, result.Command!.Kind);
    }

    [Theory]
    [InlineData("help")]
    [InlineData("--help")]
    [InlineData("-h")]
    public void Parse_HelpArgument_ReturnsHelpCommand(string argument)
    {
        CountdownCliParseResult result = CountdownCliCommandParser.Parse(new[] { argument });

        Assert.True(result.IsSuccess);
        Assert.Equal(CountdownCliCommandKind.Help, result.Command!.Kind);
    }

    [Fact]
    public void Parse_AddWithRequiredOptions_ReturnsAddCommand()
    {
        string[] arguments =
        [
            "add",
            "--title",
            "Submit report",
            "--at",
            "2026-08-01T17:00:00+08:00",
            "--timezone",
            "Asia/Shanghai",
            "--json"
        ];

        CountdownCliParseResult result = CountdownCliCommandParser.Parse(arguments);

        Assert.True(result.IsSuccess);
        Assert.Equal(CountdownCliCommandKind.Add, result.Command!.Kind);
        Assert.True(result.Command.UseJson);
        Assert.Equal("Submit report", result.Command.AddRequest!.Title);
        Assert.Equal(new DateTimeOffset(2026, 8, 1, 17, 0, 0, TimeSpan.FromHours(8)), result.Command.AddRequest.TargetDate);
        Assert.Equal("Asia/Shanghai", result.Command.AddRequest.TargetTimeZoneId);
    }

    [Fact]
    public void Parse_RequestIdOption_PopulatesAddRequest()
    {
        string[] arguments =
        [
            "add",
            "--title",
            "Launch",
            "--at",
            "2026-08-01T09:00:00Z",
            "--request-id",
            "97eabec38c7b43cbb7e121d0d4fbfdca"
        ];

        CountdownCliParseResult result = CountdownCliCommandParser.Parse(arguments);

        Assert.True(result.IsSuccess);
        Assert.Equal("97eabec38c7b43cbb7e121d0d4fbfdca", result.Command!.AddRequest!.RequestId);
    }

    [Fact]
    public void Parse_WithoutRequestIdOption_LeavesRequestIdNull()
    {
        string[] arguments = ["add", "--title", "Launch", "--at", "2026-08-01T09:00:00Z"];

        CountdownCliParseResult result = CountdownCliCommandParser.Parse(arguments);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Command!.AddRequest!.RequestId);
    }

    [Fact]
    public void Parse_UtcTarget_ReturnsAddCommand()
    {
        string[] arguments = ["add", "--title", "Launch", "--at", "2026-08-01T09:00:00Z"];

        CountdownCliParseResult result = CountdownCliCommandParser.Parse(arguments);

        Assert.True(result.IsSuccess);
        Assert.Equal(TimeSpan.Zero, result.Command!.AddRequest!.TargetDate.Offset);
    }

    [Fact]
    public void Parse_FractionalSeconds_ReturnsAddCommand()
    {
        string[] arguments = ["add", "--title", "Launch", "--at", "2026-08-01T09:00:00.1234567Z"];

        CountdownCliParseResult result = CountdownCliCommandParser.Parse(arguments);

        Assert.True(result.IsSuccess);
        Assert.Equal(1234567, result.Command!.AddRequest!.TargetDate.TimeOfDay.Ticks % TimeSpan.TicksPerSecond);
    }

    [Theory]
    [InlineData("2026-08-01")]
    [InlineData("2026-08-01T09:00:00")]
    [InlineData("2026-08-01T9:00:00Z")]
    [InlineData("2026-08-01T09:00:00.Z")]
    [InlineData("2026-08-01T09:00:00+0800")]
    [InlineData("2026-02-30T09:00:00Z")]
    [InlineData("tomorrow")]
    public void Parse_TargetWithoutExplicitOffset_ReturnsInvalidTargetDate(string target)
    {
        string[] arguments = ["add", "--title", "Launch", "--at", target];

        CountdownCliParseResult result = CountdownCliCommandParser.Parse(arguments);

        Assert.False(result.IsSuccess);
        Assert.Equal(CountdownCliParseError.InvalidTargetDate, result.Error);
    }

    [Fact]
    public void Parse_MissingTitle_ReturnsMissingTitle()
    {
        string[] arguments = ["add", "--at", "2026-08-01T09:00:00Z"];

        CountdownCliParseResult result = CountdownCliCommandParser.Parse(arguments);

        Assert.Equal(CountdownCliParseError.MissingTitle, result.Error);
    }

    [Fact]
    public void Parse_MissingTargetDate_ReturnsMissingTargetDate()
    {
        string[] arguments = ["add", "--title", "Launch"];

        CountdownCliParseResult result = CountdownCliCommandParser.Parse(arguments);

        Assert.Equal(CountdownCliParseError.MissingTargetDate, result.Error);
    }

    [Fact]
    public void Parse_MissingOptionValue_ReturnsMissingOptionValue()
    {
        string[] arguments = ["add", "--title", "--at", "2026-08-01T09:00:00Z"];

        CountdownCliParseResult result = CountdownCliCommandParser.Parse(arguments);

        Assert.Equal(CountdownCliParseError.MissingOptionValue, result.Error);
        Assert.Equal("--title", result.ErrorArgument);
    }

    [Fact]
    public void Parse_DuplicateOption_ReturnsDuplicateOption()
    {
        string[] arguments = ["add", "--title", "One", "--title", "Two", "--at", "2026-08-01T09:00:00Z"];

        CountdownCliParseResult result = CountdownCliCommandParser.Parse(arguments);

        Assert.Equal(CountdownCliParseError.DuplicateOption, result.Error);
        Assert.Equal("--title", result.ErrorArgument);
    }

    [Fact]
    public void Parse_UnknownOption_ReturnsUnknownOption()
    {
        string[] arguments = ["add", "--name", "Launch"];

        CountdownCliParseResult result = CountdownCliCommandParser.Parse(arguments);

        Assert.Equal(CountdownCliParseError.UnknownOption, result.Error);
        Assert.Equal("--name", result.ErrorArgument);
    }

    [Fact]
    public void Parse_UnknownCommand_ReturnsUnknownCommand()
    {
        CountdownCliParseResult result = CountdownCliCommandParser.Parse(new[] { "remove" });

        Assert.Equal(CountdownCliParseError.UnknownCommand, result.Error);
        Assert.Equal("remove", result.ErrorArgument);
    }
}
