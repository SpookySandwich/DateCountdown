using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace DateCountdown.Core;

public enum CountdownCliCommandKind
{
    Help,
    Add
}

public enum CountdownCliParseError
{
    None,
    UnknownCommand,
    UnknownOption,
    MissingOptionValue,
    DuplicateOption,
    MissingTitle,
    MissingTargetDate,
    InvalidTargetDate
}

public sealed record CountdownCliCommand(
    CountdownCliCommandKind Kind,
    CountdownAddRequest? AddRequest = null,
    bool UseJson = false);

public sealed record CountdownCliParseResult(
    CountdownCliCommand? Command,
    CountdownCliParseError Error,
    string? ErrorArgument = null)
{
    public bool IsSuccess => Error == CountdownCliParseError.None && Command is not null;
}

public static class CountdownCliCommandParser
{
    private static readonly Regex TargetDatePattern = new(
        @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?(?:Z|[+-]\d{2}:\d{2})$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    private const string AddCommand = "add";
    private const string AtOption = "--at";
    private const string HelpOption = "--help";
    private const string JsonOption = "--json";
    private const string RequestIdOption = "--request-id";
    private const string ShortHelpOption = "-h";
    private const string TimeZoneOption = "--timezone";
    private const string TitleOption = "--title";

    public static CountdownCliParseResult Parse(IReadOnlyList<string>? arguments)
    {
        if (arguments is null || arguments.Count == 0 || IsHelp(arguments[0]))
        {
            return Success(new CountdownCliCommand(CountdownCliCommandKind.Help));
        }

        if (!string.Equals(arguments[0], AddCommand, StringComparison.Ordinal))
        {
            return Failure(CountdownCliParseError.UnknownCommand, arguments[0]);
        }

        if (arguments.Count == 2 && IsHelp(arguments[1]))
        {
            return Success(new CountdownCliCommand(CountdownCliCommandKind.Help));
        }

        return ParseAdd(arguments);
    }

    private static CountdownCliParseResult ParseAdd(IReadOnlyList<string> arguments)
    {
        Dictionary<string, string> values = new(StringComparer.Ordinal);
        bool useJson = false;

        for (int index = 1; index < arguments.Count; index++)
        {
            string option = arguments[index];
            if (string.Equals(option, JsonOption, StringComparison.Ordinal))
            {
                if (useJson)
                {
                    return Failure(CountdownCliParseError.DuplicateOption, option);
                }

                useJson = true;
                continue;
            }

            if (!IsValueOption(option))
            {
                return Failure(CountdownCliParseError.UnknownOption, option);
            }

            if (values.ContainsKey(option))
            {
                return Failure(CountdownCliParseError.DuplicateOption, option);
            }

            if (index + 1 >= arguments.Count || IsRecognizedOption(arguments[index + 1]))
            {
                return Failure(CountdownCliParseError.MissingOptionValue, option);
            }

            values.Add(option, arguments[++index]);
        }

        if (!values.TryGetValue(TitleOption, out string? title))
        {
            return Failure(CountdownCliParseError.MissingTitle, TitleOption);
        }

        if (!values.TryGetValue(AtOption, out string? targetText))
        {
            return Failure(CountdownCliParseError.MissingTargetDate, AtOption);
        }

        if (!TryParseTargetDate(targetText, out DateTimeOffset targetDate))
        {
            return Failure(CountdownCliParseError.InvalidTargetDate, targetText);
        }

        values.TryGetValue(TimeZoneOption, out string? targetTimeZoneId);
        values.TryGetValue(RequestIdOption, out string? requestId);
        CountdownAddRequest request = new(title, targetDate, targetTimeZoneId, requestId);
        return Success(new CountdownCliCommand(CountdownCliCommandKind.Add, request, useJson));
    }

    private static bool TryParseTargetDate(string value, out DateTimeOffset targetDate)
    {
        targetDate = default;
        return TargetDatePattern.IsMatch(value) &&
            DateTimeOffset.TryParseExact(
                value,
                "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out targetDate);
    }

    private static bool IsValueOption(string value)
    {
        return string.Equals(value, TitleOption, StringComparison.Ordinal) ||
            string.Equals(value, AtOption, StringComparison.Ordinal) ||
            string.Equals(value, TimeZoneOption, StringComparison.Ordinal) ||
            string.Equals(value, RequestIdOption, StringComparison.Ordinal);
    }

    private static bool IsRecognizedOption(string value)
    {
        return IsValueOption(value) ||
            string.Equals(value, JsonOption, StringComparison.Ordinal) ||
            IsHelp(value);
    }

    private static bool IsHelp(string value)
    {
        return string.Equals(value, HelpOption, StringComparison.Ordinal) ||
            string.Equals(value, ShortHelpOption, StringComparison.Ordinal) ||
            string.Equals(value, "help", StringComparison.Ordinal);
    }

    private static CountdownCliParseResult Success(CountdownCliCommand command)
    {
        return new CountdownCliParseResult(command, CountdownCliParseError.None);
    }

    private static CountdownCliParseResult Failure(CountdownCliParseError error, string? argument)
    {
        return new CountdownCliParseResult(null, error, argument);
    }
}
