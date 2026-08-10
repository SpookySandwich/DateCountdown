using DateCountdown.Core;
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace DateCountdown.Tests;

public sealed class CountdownAutomationProtocolTests
{
    [Fact]
    public async Task WriteRequestAsync_ValidRequest_RoundTrips()
    {
        CountdownAutomationRequest request = new(
            CountdownAutomationRequest.CurrentVersion,
            new CountdownAddRequest("Launch", DateTimeOffset.UnixEpoch.AddDays(1), "UTC"));
        using MemoryStream stream = new();

        await CountdownAutomationProtocol.WriteRequestAsync(stream, request);
        stream.Position = 0;
        CountdownAutomationRequest restored = await CountdownAutomationProtocol.ReadRequestAsync(stream);

        Assert.Equal(request, restored);
    }

    [Fact]
    public async Task WriteResponseAsync_ValidResponse_RoundTrips()
    {
        CountdownAutomationResponse response = new(
            true,
            CountdownAddError.None.ToString(),
            "countdown-id",
            "Launch",
            DateTimeOffset.UnixEpoch.AddDays(1),
            "UTC");
        using MemoryStream stream = new();

        await CountdownAutomationProtocol.WriteResponseAsync(stream, response);
        stream.Position = 0;
        CountdownAutomationResponse restored = await CountdownAutomationProtocol.ReadResponseAsync(stream);

        Assert.Equal(response, restored);
    }

    [Fact]
    public async Task ReadRequestAsync_OversizedLength_ThrowsInvalidDataException()
    {
        using MemoryStream stream = new(new byte[] { 0x01, 0x00, 0x01, 0x00 });

        await Assert.ThrowsAsync<InvalidDataException>(() => CountdownAutomationProtocol.ReadRequestAsync(stream));
    }

    [Fact]
    public void FromResult_Success_ReturnsCountdownDetails()
    {
        DateTimeOffset now = DateTimeOffset.UnixEpoch;
        CountdownAddResult result = CountdownAddCommand.Execute(
            CountdownState.CreateEmpty(),
            new CountdownAddRequest("Launch", now.AddDays(1), "UTC"),
            now);

        CountdownAutomationResponse response = CountdownAutomationResponse.FromResult(result);

        Assert.True(response.IsSuccess);
        Assert.Equal(CountdownAddError.None.ToString(), response.Code);
        Assert.Equal(result.AddedCountdown!.Id, response.CountdownId);
        Assert.Equal("Launch", response.Title);
    }

    [Fact]
    public void FromResult_Failure_ReturnsStableErrorCode()
    {
        DateTimeOffset now = DateTimeOffset.UnixEpoch;
        CountdownAddResult result = CountdownAddCommand.Execute(
            CountdownState.CreateEmpty(),
            new CountdownAddRequest(string.Empty, now.AddDays(1), "UTC"),
            now);

        CountdownAutomationResponse response = CountdownAutomationResponse.FromResult(result);

        Assert.False(response.IsSuccess);
        Assert.Equal(nameof(CountdownAddError.TitleRequired), response.Code);
        Assert.Null(response.CountdownId);
    }
}
