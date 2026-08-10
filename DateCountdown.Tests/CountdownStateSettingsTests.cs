using DateCountdown.Core;
using System;
using System.Collections.Generic;
using Xunit;

namespace DateCountdown.Tests;

public sealed class CountdownStateSettingsTests
{
    [Fact]
    public void SaveThenLoad_MultipleCountdowns_RoundTripsState()
    {
        DateTimeOffset firstTarget = new(2026, 8, 1, 9, 0, 0, TimeSpan.Zero);
        DateTimeOffset secondTarget = firstTarget.AddDays(1);
        CountdownState state = new(
            new[]
            {
                new CountdownItem("first", "First", firstTarget, toastEnabled: true, "UTC"),
                new CountdownItem("second", "Second", secondTarget, toastEnabled: false, "UTC")
            },
            "second",
            tileEnabled: true,
            toastEnabled: false);
        Dictionary<string, object> values = new();

        CountdownStateSettings.Save(values, state);
        CountdownState restored = CountdownStateSettings.Load(values, DateTimeOffset.UnixEpoch);

        Assert.Equal(2, restored.Countdowns.Count);
        Assert.Equal("second", restored.SelectedCountdownId);
        Assert.Equal("Second", restored.Title);
        Assert.True(restored.TileEnabled);
        Assert.True(restored.AnyToastEnabled);
    }

    [Fact]
    public void Load_LegacyValues_ReturnsLegacyCountdown()
    {
        DateTimeOffset target = new(2026, 8, 1, 9, 0, 0, TimeSpan.Zero);
        Dictionary<string, object> values = new()
        {
            [CountdownSettingsKeys.Title] = "Legacy",
            [CountdownSettingsKeys.TargetDate] = target,
            [CountdownSettingsKeys.TargetTimeZoneId] = "UTC",
            [CountdownSettingsKeys.TileEnabled] = true
        };

        CountdownState state = CountdownStateSettings.Load(values, DateTimeOffset.UnixEpoch);

        Assert.Single(state.Countdowns);
        Assert.Equal("Legacy", state.Title);
        Assert.Equal(target, state.TargetDate);
        Assert.True(state.TileEnabled);
    }

    [Fact]
    public void Save_EmptyState_RemovesLegacyTargetAndDisablesFeatures()
    {
        Dictionary<string, object> values = new()
        {
            [CountdownSettingsKeys.TargetDate] = DateTimeOffset.UnixEpoch,
            [CountdownSettingsKeys.TileEnabled] = true,
            [CountdownSettingsKeys.ToastEnabled] = true
        };

        CountdownStateSettings.Save(values, CountdownState.CreateEmpty());

        Assert.False(values.ContainsKey(CountdownSettingsKeys.TargetDate));
        Assert.Equal(false, values[CountdownSettingsKeys.TileEnabled]);
        Assert.Equal(false, values[CountdownSettingsKeys.ToastEnabled]);
    }

    [Fact]
    public void Update_ValidTransition_PersistsUpdatedState()
    {
        Dictionary<string, object> values = new();
        DateTimeOffset now = new(2026, 7, 17, 12, 0, 0, TimeSpan.Zero);

        CountdownState updated = CountdownStateSettings.Update(
            values,
            CountdownTimeZone.CreateDefaultTargetDate(now),
            state => CountdownAddCommand.Execute(
                state,
                new CountdownAddRequest("Launch", now.AddDays(1), "UTC"),
                now).State);
        CountdownState restored = CountdownStateSettings.Load(values, DateTimeOffset.UnixEpoch);

        Assert.Single(updated.Countdowns);
        Assert.Equal("Launch", restored.Title);
        Assert.Equal(updated.SelectedCountdownId, restored.SelectedCountdownId);
    }

    [Fact]
    public void Update_NoOp_DoesNotWriteSettings()
    {
        Dictionary<string, object> values = new();

        CountdownState state = CountdownStateSettings.Update(
            values,
            DateTimeOffset.UnixEpoch,
            current => current);

        Assert.Empty(values);
        Assert.Empty(state.Countdowns);
    }
}
