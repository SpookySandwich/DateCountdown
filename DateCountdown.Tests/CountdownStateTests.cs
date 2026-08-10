using DateCountdown.Core;
using System;
using System.Linq;
using Xunit;

namespace DateCountdown.Tests;

public sealed class CountdownStateTests
{
    [Fact]
    public void Constructor_NormalizesNullTitleToEmptyString()
    {
        CountdownState state = new(null!, DateTimeOffset.UnixEpoch, tileEnabled: false, toastEnabled: false);

        Assert.Equal(string.Empty, state.Title);
    }

    [Fact]
    public void CreateEmpty_ReturnsStateWithoutCountdowns()
    {
        CountdownState state = CountdownState.CreateEmpty();

        Assert.Empty(state.Countdowns);
        Assert.False(state.HasCountdowns);
        Assert.Equal(string.Empty, state.SelectedCountdownId);
        Assert.Null(state.SelectedCountdown);
        Assert.Equal(string.Empty, state.Title);
        Assert.Equal(default, state.TargetDate);
        Assert.Equal(CountdownTimeZone.DefaultTimeZoneId, state.TargetTimeZoneId);
        Assert.False(state.TileEnabled);
        Assert.False(state.ToastEnabled);
        Assert.False(state.AnyToastEnabled);
    }

    [Fact]
    public void CreateDefault_ReturnsEmptyState()
    {
        CountdownState state = CountdownState.CreateDefault(new DateTimeOffset(2026, 5, 8, 0, 0, 0, TimeSpan.Zero));

        Assert.Empty(state.Countdowns);
        Assert.False(state.HasCountdowns);
    }

    [Fact]
    public void With_PreservesUnspecifiedValues()
    {
        DateTimeOffset targetDate = new(2026, 5, 8, 0, 0, 0, TimeSpan.Zero);
        CountdownState original = new("Launch", targetDate, tileEnabled: true, toastEnabled: false);

        CountdownState updated = original.With(toastEnabled: true);

        Assert.Equal("Launch", updated.Title);
        Assert.Equal(targetDate, updated.TargetDate);
        Assert.True(updated.TileEnabled);
        Assert.True(updated.ToastEnabled);
        Assert.True(updated.AnyToastEnabled);
    }

    [Fact]
    public void With_UpdatesSelectedCountdownToastAndPreservesOtherCountdowns()
    {
        CountdownState state = new CountdownState(
            new[]
            {
                new CountdownItem("first", "First", DateTimeOffset.UnixEpoch, toastEnabled: true),
                new CountdownItem("second", "Second", DateTimeOffset.UnixEpoch.AddDays(1), toastEnabled: false)
            },
            selectedCountdownId: "second",
            tileEnabled: false,
            toastEnabled: false);

        CountdownState updated = state.With(toastEnabled: true);

        Assert.True(updated.ToastEnabled);
        Assert.True(updated.Countdowns[0].ToastEnabled);
        Assert.True(updated.Countdowns[1].ToastEnabled);
        Assert.True(updated.AnyToastEnabled);
    }

    [Fact]
    public void With_AllowsExplicitEmptyTitle()
    {
        CountdownState original = new("Launch", DateTimeOffset.UnixEpoch, tileEnabled: true, toastEnabled: true);

        CountdownState updated = original.With(title: string.Empty);

        Assert.Equal(string.Empty, updated.Title);
    }

    [Fact]
    public void MultiItemConstructor_SelectsRequestedCountdown()
    {
        DateTimeOffset firstDate = new(2026, 5, 8, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset secondDate = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

        CountdownState state = new CountdownState(
            new[]
            {
                new CountdownItem("first", "First", firstDate),
                new CountdownItem("second", "Second", secondDate)
            },
            selectedCountdownId: "second",
            tileEnabled: false,
            toastEnabled: false);

        Assert.Equal("second", state.SelectedCountdownId);
        Assert.Equal("Second", state.Title);
        Assert.Equal(secondDate, state.TargetDate);
    }

    [Fact]
    public void MultiItemConstructor_FallsBackToFirstCountdownForMissingSelection()
    {
        CountdownState state = new CountdownState(
            new[]
            {
                new CountdownItem("first", "First", DateTimeOffset.UnixEpoch),
                new CountdownItem("second", "Second", DateTimeOffset.UnixEpoch.AddDays(1))
            },
            selectedCountdownId: "missing",
            tileEnabled: false,
            toastEnabled: false);

        Assert.Equal("first", state.SelectedCountdownId);
        Assert.Equal("First", state.Title);
    }

    [Fact]
    public void MultiItemConstructor_MigratesLegacyToastFlagToSelectedCountdown()
    {
        CountdownState state = new CountdownState(
            new[]
            {
                new CountdownItem("first", "First", DateTimeOffset.UnixEpoch),
                new CountdownItem("second", "Second", DateTimeOffset.UnixEpoch.AddDays(1))
            },
            selectedCountdownId: "second",
            tileEnabled: false,
            toastEnabled: true);

        Assert.True(state.ToastEnabled);
        Assert.False(state.Countdowns[0].ToastEnabled);
        Assert.True(state.Countdowns[1].ToastEnabled);
        Assert.True(state.AnyToastEnabled);
    }

    [Fact]
    public void AddCountdown_AppendsAndSelectsNewCountdown()
    {
        CountdownState state = new("First", DateTimeOffset.UnixEpoch, tileEnabled: false, toastEnabled: false);
        CountdownItem second = new("second", "Second", DateTimeOffset.UnixEpoch.AddDays(1));

        CountdownState updated = state.AddCountdown(second, selectCountdown: true);

        Assert.Equal(2, updated.Countdowns.Count);
        Assert.Equal("second", updated.SelectedCountdownId);
        Assert.Equal("Second", updated.Title);
    }

    [Fact]
    public void AddCountdown_RegeneratesDuplicateIds()
    {
        CountdownState state = new("First", DateTimeOffset.UnixEpoch, tileEnabled: false, toastEnabled: false);
        CountdownItem duplicate = new(CountdownItem.DefaultId, "Second", DateTimeOffset.UnixEpoch.AddDays(1));

        CountdownState updated = state.AddCountdown(duplicate, selectCountdown: true);

        Assert.Equal(2, updated.Countdowns.Count);
        Assert.NotEqual(CountdownItem.DefaultId, updated.Countdowns[1].Id);
        Assert.Equal(updated.Countdowns[1].Id, updated.SelectedCountdownId);
    }

    [Fact]
    public void AddCountdown_IgnoresNullCountdown()
    {
        CountdownState state = new("First", DateTimeOffset.UnixEpoch, tileEnabled: false, toastEnabled: false);

        CountdownState updated = state.AddCountdown(null, selectCountdown: true);

        Assert.Same(state, updated);
    }

    [Fact]
    public void AddCountdown_OnEmptyStateAppendsAndSelectsNewCountdown()
    {
        CountdownState state = CountdownState.CreateEmpty();
        CountdownItem countdown = new("first", "First", DateTimeOffset.UnixEpoch);

        CountdownState updated = state.AddCountdown(countdown, selectCountdown: false);

        Assert.Single(updated.Countdowns);
        Assert.True(updated.HasCountdowns);
        Assert.Equal("first", updated.SelectedCountdownId);
        Assert.Equal("First", updated.Title);
    }

    [Fact]
    public void AddDefaultCountdown_CreatesNamedCountdownAndSelectsIt()
    {
        CountdownState state = new("First", DateTimeOffset.UnixEpoch, tileEnabled: true, toastEnabled: false);
        DateTimeOffset targetDate = DateTimeOffset.UnixEpoch.AddDays(5);

        CountdownState updated = state.AddDefaultCountdown(targetDate, number => $"Countdown {number}");

        Assert.Equal(2, updated.Countdowns.Count);
        Assert.Equal(updated.Countdowns[1].Id, updated.SelectedCountdownId);
        Assert.Equal("Countdown 2", updated.Title);
        Assert.Equal(targetDate, updated.TargetDate);
        Assert.True(updated.TileEnabled);
    }

    [Fact]
    public void AddDefaultCountdown_PreservesExistingCountdownNotifications()
    {
        CountdownState state = new("First", DateTimeOffset.UnixEpoch, tileEnabled: false, toastEnabled: true);

        CountdownState updated = state.AddDefaultCountdown(
            DateTimeOffset.UnixEpoch.AddDays(5),
            number => $"Countdown {number}");

        Assert.False(updated.ToastEnabled);
        Assert.True(updated.AnyToastEnabled);
        Assert.True(updated.Countdowns[0].ToastEnabled);
        Assert.False(updated.Countdowns[1].ToastEnabled);
    }

    [Fact]
    public void AddDefaultCountdown_SkipsExistingDefaultTitles()
    {
        CountdownState state = new CountdownState(
            new[]
            {
                new CountdownItem("first", "Countdown 1", DateTimeOffset.UnixEpoch),
                new CountdownItem("third", "Countdown 3", DateTimeOffset.UnixEpoch.AddDays(1))
            },
            selectedCountdownId: "first",
            tileEnabled: false,
            toastEnabled: false);

        CountdownState updated = state.AddDefaultCountdown(
            DateTimeOffset.UnixEpoch.AddDays(5),
            number => $"Countdown {number}");

        Assert.Equal("Countdown 4", updated.Title);
    }

    [Fact]
    public void WithDefaultTitles_NamesOnlyBlankCountdowns()
    {
        CountdownState state = new CountdownState(
            new[]
            {
                new CountdownItem("first", string.Empty, DateTimeOffset.UnixEpoch, toastEnabled: true),
                new CountdownItem("second", "Custom", DateTimeOffset.UnixEpoch.AddDays(1), toastEnabled: false),
                new CountdownItem("third", "   ", DateTimeOffset.UnixEpoch.AddDays(2), toastEnabled: true)
            },
            selectedCountdownId: "third",
            tileEnabled: true,
            toastEnabled: false);

        CountdownState updated = state.WithDefaultTitles(number => $"Countdown {number}");

        Assert.Equal(new[] { "Countdown 1", "Custom", "Countdown 3" }, updated.Countdowns.Select(item => item.Title));
        Assert.Equal("third", updated.SelectedCountdownId);
        Assert.Equal("Countdown 3", updated.Title);
        Assert.True(updated.TileEnabled);
        Assert.True(updated.Countdowns[0].ToastEnabled);
        Assert.True(updated.Countdowns[2].ToastEnabled);
    }

    [Fact]
    public void WithDefaultTitles_SkipsExistingDefaultTitles()
    {
        CountdownState state = new CountdownState(
            new[]
            {
                new CountdownItem("first", string.Empty, DateTimeOffset.UnixEpoch),
                new CountdownItem("second", "Countdown 1", DateTimeOffset.UnixEpoch.AddDays(1)),
                new CountdownItem("third", "   ", DateTimeOffset.UnixEpoch.AddDays(2))
            },
            selectedCountdownId: "third",
            tileEnabled: false,
            toastEnabled: false);

        CountdownState updated = state.WithDefaultTitles(number => $"Countdown {number}");

        Assert.Equal(new[] { "Countdown 2", "Countdown 1", "Countdown 3" }, updated.Countdowns.Select(item => item.Title));
        Assert.Equal("third", updated.SelectedCountdownId);
        Assert.Equal("Countdown 3", updated.Title);
    }

    [Fact]
    public void WithDefaultTitles_ReturnsSameStateWhenTitlesExist()
    {
        CountdownState state = new("First", DateTimeOffset.UnixEpoch, tileEnabled: false, toastEnabled: false);

        CountdownState updated = state.WithDefaultTitles(number => $"Countdown {number}");

        Assert.Same(state, updated);
    }

    [Fact]
    public void MultiItemConstructor_IgnoresNullCountdowns()
    {
        CountdownState state = new CountdownState(
            new CountdownItem?[]
            {
                null,
                new CountdownItem("first", "First", DateTimeOffset.UnixEpoch)
            },
            selectedCountdownId: "first",
            tileEnabled: false,
            toastEnabled: false);

        Assert.Single(state.Countdowns);
        Assert.Equal("first", state.SelectedCountdownId);
    }

    [Fact]
    public void MultiItemConstructor_AllowsEmptyCountdowns()
    {
        CountdownState state = new CountdownState(null, selectedCountdownId: "missing", tileEnabled: true, toastEnabled: true);

        Assert.Empty(state.Countdowns);
        Assert.False(state.HasCountdowns);
        Assert.Equal(string.Empty, state.SelectedCountdownId);
        Assert.False(state.TileEnabled);
        Assert.False(state.ToastEnabled);
        Assert.False(state.AnyToastEnabled);
    }

    [Fact]
    public void SelectCountdown_ChangesSelectedCountdownWhenIdExists()
    {
        CountdownState state = new CountdownState(
            new[]
            {
                new CountdownItem("first", "First", DateTimeOffset.UnixEpoch),
                new CountdownItem("second", "Second", DateTimeOffset.UnixEpoch.AddDays(1))
            },
            selectedCountdownId: "first",
            tileEnabled: true,
            toastEnabled: true);

        CountdownState updated = state.SelectCountdown("second");

        Assert.Equal("second", updated.SelectedCountdownId);
        Assert.Equal("Second", updated.Title);
        Assert.True(updated.TileEnabled);
        Assert.False(updated.ToastEnabled);
        Assert.True(updated.AnyToastEnabled);
    }

    [Fact]
    public void SelectCountdown_IgnoresMissingIds()
    {
        CountdownState state = new("First", DateTimeOffset.UnixEpoch, tileEnabled: false, toastEnabled: false);

        CountdownState updated = state.SelectCountdown("missing");

        Assert.Same(state, updated);
    }

    [Fact]
    public void CanRemoveCountdown_IsFalseForEmptyState()
    {
        CountdownState state = CountdownState.CreateEmpty();

        Assert.False(state.CanRemoveCountdown);
    }

    [Fact]
    public void CanRemoveCountdown_IsTrueForSingleCountdown()
    {
        CountdownState state = new("First", DateTimeOffset.UnixEpoch, tileEnabled: false, toastEnabled: false);

        Assert.True(state.CanRemoveCountdown);
    }

    [Fact]
    public void CanRemoveCountdown_IsTrueForMultipleCountdowns()
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

        Assert.True(state.CanRemoveCountdown);
    }

    [Fact]
    public void RemoveCountdown_RemovesSingleCountdown()
    {
        CountdownState state = new("First", DateTimeOffset.UnixEpoch, tileEnabled: false, toastEnabled: false);

        CountdownState updated = state.RemoveCountdown(CountdownItem.DefaultId);

        Assert.Empty(updated.Countdowns);
        Assert.False(updated.HasCountdowns);
        Assert.Equal(string.Empty, updated.SelectedCountdownId);
        Assert.False(updated.TileEnabled);
        Assert.False(updated.AnyToastEnabled);
    }

    [Fact]
    public void RemoveCountdown_IgnoresMissingIds()
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

        CountdownState updated = state.RemoveCountdown("missing");

        Assert.Same(state, updated);
    }

    [Fact]
    public void RemoveCountdown_PreservesSelectionWhenRemovingAnotherCountdown()
    {
        CountdownState state = new CountdownState(
            new[]
            {
                new CountdownItem("first", "First", DateTimeOffset.UnixEpoch),
                new CountdownItem("second", "Second", DateTimeOffset.UnixEpoch.AddDays(1)),
                new CountdownItem("third", "Third", DateTimeOffset.UnixEpoch.AddDays(2))
            },
            selectedCountdownId: "second",
            tileEnabled: true,
            toastEnabled: true);

        CountdownState updated = state.RemoveCountdown("first");

        Assert.Equal(2, updated.Countdowns.Count);
        Assert.Equal("second", updated.SelectedCountdownId);
        Assert.Equal("Second", updated.Title);
        Assert.True(updated.TileEnabled);
        Assert.True(updated.ToastEnabled);
    }

    [Fact]
    public void RemoveCountdown_SelectsNextCountdownWhenRemovingSelectedCountdown()
    {
        CountdownState state = new CountdownState(
            new[]
            {
                new CountdownItem("first", "First", DateTimeOffset.UnixEpoch),
                new CountdownItem("second", "Second", DateTimeOffset.UnixEpoch.AddDays(1)),
                new CountdownItem("third", "Third", DateTimeOffset.UnixEpoch.AddDays(2))
            },
            selectedCountdownId: "second",
            tileEnabled: false,
            toastEnabled: false);

        CountdownState updated = state.RemoveCountdown("second");

        Assert.Equal(2, updated.Countdowns.Count);
        Assert.Equal("third", updated.SelectedCountdownId);
        Assert.Equal("Third", updated.Title);
    }

    [Fact]
    public void RemoveCountdown_SelectsPreviousCountdownWhenRemovingLastSelectedCountdown()
    {
        CountdownState state = new CountdownState(
            new[]
            {
                new CountdownItem("first", "First", DateTimeOffset.UnixEpoch),
                new CountdownItem("second", "Second", DateTimeOffset.UnixEpoch.AddDays(1))
            },
            selectedCountdownId: "second",
            tileEnabled: false,
            toastEnabled: false);

        CountdownState updated = state.RemoveCountdown("second");

        Assert.Single(updated.Countdowns);
        Assert.Equal("first", updated.SelectedCountdownId);
        Assert.Equal("First", updated.Title);
        Assert.True(updated.CanRemoveCountdown);
    }

    [Fact]
    public void RemoveExpiredCountdowns_RemovesPastDatesAndKeepsActiveSettings()
    {
        DateTimeOffset now = new(2026, 4, 25, 12, 0, 0, TimeSpan.Zero);
        CountdownState state = new CountdownState(
            new[]
            {
                new CountdownItem("expired", "Expired", now.AddDays(-1), toastEnabled: true),
                new CountdownItem("today", "Today", now, toastEnabled: false),
                new CountdownItem("later", "Later", now.AddDays(2), toastEnabled: true)
            },
            selectedCountdownId: "expired",
            tileEnabled: true,
            toastEnabled: false);

        CountdownState updated = state.RemoveExpiredCountdowns(now);

        Assert.Equal(new[] { "today", "later" }, updated.Countdowns.Select(item => item.Id));
        Assert.Equal("today", updated.SelectedCountdownId);
        Assert.True(updated.TileEnabled);
        Assert.False(updated.Countdowns[0].ToastEnabled);
        Assert.True(updated.Countdowns[1].ToastEnabled);
    }

    [Fact]
    public void RemoveExpiredCountdowns_ReturnsEmptyStateWhenAllCountdownsExpired()
    {
        DateTimeOffset now = new(2026, 4, 25, 12, 0, 0, TimeSpan.FromHours(8));
        CountdownState state = new CountdownState(
            new[] { new CountdownItem("expired", "Expired", now.AddDays(-1), toastEnabled: true) },
            selectedCountdownId: "expired",
            tileEnabled: true,
            toastEnabled: false);

        CountdownState updated = state.RemoveExpiredCountdowns(now);

        Assert.Empty(updated.Countdowns);
        Assert.False(updated.HasCountdowns);
        Assert.Equal(string.Empty, updated.SelectedCountdownId);
        Assert.Equal(string.Empty, updated.Title);
        Assert.Equal(default, updated.TargetDate);
        Assert.False(updated.TileEnabled);
        Assert.False(updated.AnyToastEnabled);
    }

    [Fact]
    public void RemoveExpiredCountdowns_ReturnsSameStateWhenNothingExpired()
    {
        DateTimeOffset now = new(2026, 4, 25, 12, 0, 0, TimeSpan.Zero);
        CountdownState state = new("Today", now, tileEnabled: false, toastEnabled: false);

        Assert.Same(state, state.RemoveExpiredCountdowns(now));
    }

    [Fact]
    public void SortByDaysLeft_OrdersByRemainingDaysAndPreservesSelection()
    {
        DateTimeOffset now = new(2026, 4, 26, 0, 0, 0, TimeSpan.Zero);
        CountdownState state = new CountdownState(
            new[]
            {
                new CountdownItem("later", "Later", now.AddDays(30)),
                new CountdownItem("soon", "Soon", now.AddDays(5)),
                new CountdownItem("today", "Today", now)
            },
            selectedCountdownId: "later",
            tileEnabled: true,
            toastEnabled: false);

        CountdownState sorted = state.SortByDaysLeft(now);

        Assert.Equal(new[] { "today", "soon", "later" }, sorted.Countdowns.Select(item => item.Id));
        Assert.Equal("later", sorted.SelectedCountdownId);
        Assert.Equal("Later", sorted.Title);
        Assert.True(sorted.TileEnabled);
    }
}
