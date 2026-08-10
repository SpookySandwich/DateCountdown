using DateCountdown.Core;
using Microsoft.Windows.ApplicationModel.Resources;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;

namespace DateCountdown.Cli;

internal sealed class CountdownCliApplication
{
    private const int SuccessExitCode = 0;
    private const int UsageErrorExitCode = 2;
    private const int ValidationErrorExitCode = 3;
    private const int RuntimeErrorExitCode = 4;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly TextWriter _error;
    private readonly TextWriter _output;
    private readonly ResourceLoader _resourceLoader = new();
    private readonly CountdownAutomationClient _automationClient = new();

    public CountdownCliApplication(TextWriter output, TextWriter error)
    {
        _output = output ?? throw new ArgumentNullException(nameof(output));
        _error = error ?? throw new ArgumentNullException(nameof(error));
    }

    public async Task<int> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken = default)
    {
        CountdownCliParseResult parseResult = CountdownCliCommandParser.Parse(arguments);
        if (!parseResult.IsSuccess)
        {
            bool useJson = arguments.Any(argument => string.Equals(argument, "--json", StringComparison.Ordinal));
            return WriteParseError(parseResult, useJson);
        }

        CountdownCliCommand command = parseResult.Command!;
        if (command.Kind == CountdownCliCommandKind.Help)
        {
            await _output.WriteLineAsync(GetString("Cli/Help"));
            return SuccessExitCode;
        }

        return await RunAddAsync(command, cancellationToken);
    }

    private async Task<int> RunAddAsync(CountdownCliCommand command, CancellationToken cancellationToken)
    {
        CountdownAddRequest addRequest = command.AddRequest! with
        {
            RequestId = command.AddRequest!.RequestId ?? Guid.NewGuid().ToString("N")
        };
        CountdownAutomationRequest automationRequest = new(
            CountdownAutomationRequest.CurrentVersion,
            addRequest);
        CountdownAutomationResponse? response;

        try
        {
            response = await _automationClient.TrySendAsync(automationRequest, cancellationToken);
            if (response is null)
            {
                response = ExecuteDirectIfHostAbsent(addRequest);
            }

            if (response is null)
            {
                response = await _automationClient.TrySendAsync(automationRequest, cancellationToken) ??
                    new CountdownAutomationResponse(false, "HostUnavailable");
            }
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException or TimeoutException or UnauthorizedAccessException)
        {
            response = new CountdownAutomationResponse(false, "PackageUnavailable");
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            response = new CountdownAutomationResponse(false, "InternalError");
        }

        return await WriteResponseAsync(response, command.UseJson);
    }

    private static CountdownAutomationResponse? ExecuteDirectIfHostAbsent(CountdownAddRequest request)
    {
        DateTimeOffset now = DateTimeOffset.Now;
        CountdownAddResult? result = null;
        bool hostBecamePresent = false;
        ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
        CountdownStateSettings.Update(
            localSettings.Values,
            CountdownTimeZone.CreateDefaultTargetDate(now),
            state =>
            {
                if (CountdownAutomationClient.IsHostPresent())
                {
                    hostBecamePresent = true;
                    return state;
                }

                result = CountdownAddCommand.Execute(state, request, now);
                return result.State;
            });

        return hostBecamePresent ? null : CountdownAutomationResponse.FromResult(result!);
    }

    private int WriteParseError(CountdownCliParseResult result, bool useJson)
    {
        if (useJson)
        {
            WriteJson(new CountdownAutomationResponse(false, result.Error.ToString()));
            return UsageErrorExitCode;
        }

        string resourceKey = $"Cli/Error/{result.Error}";
        string message = string.Format(
            CultureInfo.CurrentCulture,
            GetString(resourceKey),
            result.ErrorArgument ?? string.Empty);
        _error.WriteLine(message);
        _error.WriteLine(GetString("Cli/HelpHint"));
        return UsageErrorExitCode;
    }

    private async Task<int> WriteResponseAsync(CountdownAutomationResponse response, bool useJson)
    {
        if (useJson)
        {
            WriteJson(response);
        }
        else if (response.IsSuccess)
        {
            string target = CountdownTimeZone.FormatTargetDateTime(
                response.TargetDate!.Value,
                response.TargetTimeZoneId,
                CultureInfo.CurrentCulture);
            await _output.WriteLineAsync(string.Format(
                CultureInfo.CurrentCulture,
                GetString("Cli/Success/Added"),
                response.Title,
                target));
        }
        else
        {
            await _error.WriteLineAsync(GetString($"Cli/Error/{response.Code}"));
        }

        if (response.IsSuccess)
        {
            return SuccessExitCode;
        }

        return Enum.TryParse(response.Code, out CountdownAddError _)
            ? ValidationErrorExitCode
            : RuntimeErrorExitCode;
    }

    private void WriteJson(CountdownAutomationResponse response)
    {
        _output.WriteLine(JsonSerializer.Serialize(response, JsonOptions));
    }

    private string GetString(string resourceName)
    {
        string value = _resourceLoader.GetString(resourceName);
        return string.IsNullOrEmpty(value) ? resourceName : value;
    }
}
