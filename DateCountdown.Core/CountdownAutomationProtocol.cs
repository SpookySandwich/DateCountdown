using System;
using System.Buffers.Binary;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DateCountdown.Core;

public sealed record CountdownAutomationRequest(int Version, CountdownAddRequest? AddRequest)
{
    public const int CurrentVersion = 1;
}

public sealed record CountdownAutomationResponse(
    bool IsSuccess,
    string Code,
    string? CountdownId = null,
    string? Title = null,
    DateTimeOffset? TargetDate = null,
    string? TargetTimeZoneId = null)
{
    public static CountdownAutomationResponse FromResult(CountdownAddResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (!result.IsSuccess || result.AddedCountdown is not CountdownItem countdown)
        {
            return new CountdownAutomationResponse(false, result.Error.ToString());
        }

        return new CountdownAutomationResponse(
            true,
            CountdownAddError.None.ToString(),
            countdown.Id,
            countdown.Title,
            countdown.TargetDate,
            countdown.TargetTimeZoneId);
    }
}

public static class CountdownAutomationProtocol
{
    public const string HostPresenceEventName = "Local\\DateCountdown.AutomationHost.v1";
    public const string PipeName = "DateCountdown.Automation.v1";

    private const int LengthPrefixSize = sizeof(int);
    private const int MaxPayloadSize = 16 * 1024;
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static Task WriteRequestAsync(
        Stream stream,
        CountdownAutomationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return WriteAsync(stream, request, cancellationToken);
    }

    public static Task<CountdownAutomationRequest> ReadRequestAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        return ReadAsync<CountdownAutomationRequest>(stream, cancellationToken);
    }

    public static Task WriteResponseAsync(
        Stream stream,
        CountdownAutomationResponse response,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(response);
        return WriteAsync(stream, response, cancellationToken);
    }

    public static Task<CountdownAutomationResponse> ReadResponseAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        return ReadAsync<CountdownAutomationResponse>(stream, cancellationToken);
    }

    private static async Task WriteAsync<T>(Stream stream, T message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);

        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(message, SerializerOptions);
        if (payload.Length > MaxPayloadSize)
        {
            throw new InvalidDataException("The automation message is too large.");
        }

        byte[] prefix = new byte[LengthPrefixSize];
        BinaryPrimitives.WriteInt32LittleEndian(prefix, payload.Length);
        await stream.WriteAsync(prefix, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<T> ReadAsync<T>(Stream stream, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);

        byte[] prefix = new byte[LengthPrefixSize];
        await stream.ReadExactlyAsync(prefix, cancellationToken).ConfigureAwait(false);
        int payloadLength = BinaryPrimitives.ReadInt32LittleEndian(prefix);
        if (payloadLength <= 0 || payloadLength > MaxPayloadSize)
        {
            throw new InvalidDataException("The automation message has an invalid size.");
        }

        byte[] payload = new byte[payloadLength];
        await stream.ReadExactlyAsync(payload, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize<T>(payload, SerializerOptions) ??
            throw new InvalidDataException("The automation message is empty.");
    }
}
