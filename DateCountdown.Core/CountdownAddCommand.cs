using System;
using System.Globalization;
using System.Linq;

namespace DateCountdown.Core;

public sealed record CountdownAddRequest(
    string? Title,
    DateTimeOffset TargetDate,
    string? TargetTimeZoneId = null,
    string? RequestId = null);

public enum CountdownAddError
{
    None,
    TitleRequired,
    TitleTooLong,
    TargetDateInPast,
    InvalidTimeZone,
    InvalidRequestId
}

public sealed record CountdownAddResult(
    CountdownState State,
    CountdownItem? AddedCountdown,
    CountdownAddError Error)
{
    public bool IsSuccess => Error == CountdownAddError.None && AddedCountdown is not null;
}

public static class CountdownAddCommand
{
    public static CountdownAddResult Execute(
        CountdownState state,
        CountdownAddRequest request,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(request);

        string countdownId;
        if (request.RequestId is null)
        {
            countdownId = Guid.NewGuid().ToString("N");
        }
        else if (Guid.TryParseExact(request.RequestId, "N", out Guid requestId))
        {
            countdownId = requestId.ToString("N");
        }
        else
        {
            return Failure(state, CountdownAddError.InvalidRequestId);
        }

        CountdownItem? existingCountdown = state.Countdowns.FirstOrDefault(countdown =>
            string.Equals(countdown.Id, countdownId, StringComparison.Ordinal));
        if (existingCountdown is not null)
        {
            return new CountdownAddResult(state, existingCountdown, CountdownAddError.None);
        }

        string title = request.Title?.Trim() ?? string.Empty;
        if (title.Length == 0)
        {
            return Failure(state, CountdownAddError.TitleRequired);
        }

        if (new StringInfo(title).LengthInTextElements > CountdownItem.MaxTitleLength)
        {
            return Failure(state, CountdownAddError.TitleTooLong);
        }

        if (request.TargetDate.ToUniversalTime() < now.ToUniversalTime())
        {
            return Failure(state, CountdownAddError.TargetDateInPast);
        }

        if (!CountdownTimeZone.TryNormalizeTimeZoneId(request.TargetTimeZoneId, out string targetTimeZoneId))
        {
            return Failure(state, CountdownAddError.InvalidTimeZone);
        }

        CountdownItem countdown = new(
            countdownId,
            title,
            request.TargetDate,
            toastEnabled: false,
            targetTimeZoneId);
        CountdownState nextState = state.AddCountdown(countdown, selectCountdown: true);

        return new CountdownAddResult(nextState, countdown, CountdownAddError.None);
    }

    private static CountdownAddResult Failure(CountdownState state, CountdownAddError error)
    {
        return new CountdownAddResult(state, null, error);
    }
}
