using System;
using System.Collections.Generic;
using System.Threading;

namespace DateCountdown.Core;

public static class CountdownStateSettings
{
    private const string SettingsMutexName = "Local\\DateCountdown.Settings.v1";
    private static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(10);

    public static CountdownState Load(IDictionary<string, object> values, DateTimeOffset fallbackDate)
    {
        ArgumentNullException.ThrowIfNull(values);
        using IDisposable guard = EnterLock();
        return LoadCore(values, fallbackDate);
    }

    public static void Save(IDictionary<string, object> values, CountdownState state)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(state);
        using IDisposable guard = EnterLock();
        SaveCore(values, state);
    }

    public static CountdownState Update(
        IDictionary<string, object> values,
        DateTimeOffset fallbackDate,
        Func<CountdownState, CountdownState> update)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(update);

        using IDisposable guard = EnterLock();
        CountdownState state = LoadCore(values, fallbackDate);
        CountdownState nextState = update(state) ??
            throw new InvalidOperationException("The countdown state update returned null.");
        if (!ReferenceEquals(state, nextState))
        {
            SaveCore(values, nextState);
        }

        return nextState;
    }

    private static CountdownState LoadCore(IDictionary<string, object> values, DateTimeOffset fallbackDate)
    {
        bool tileEnabled = ReadBool(values, CountdownSettingsKeys.TileEnabled);
        bool toastEnabled = ReadBool(values, CountdownSettingsKeys.ToastEnabled);
        IReadOnlyList<CountdownItem> countdowns = CountdownStateJson.DeserializeCountdowns(
            ReadString(values, CountdownSettingsKeys.Countdowns));
        string selectedCountdownId = ReadString(values, CountdownSettingsKeys.SelectedCountdownId) ?? string.Empty;

        if (countdowns.Count > 0)
        {
            return new CountdownState(countdowns, selectedCountdownId, tileEnabled, toastEnabled);
        }

        string title = ReadString(values, CountdownSettingsKeys.Title) ?? string.Empty;
        if (!HasLegacyCountdown(values, title))
        {
            return CountdownState.CreateEmpty();
        }

        string? targetTimeZoneId = ReadString(values, CountdownSettingsKeys.TargetTimeZoneId);
        values.TryGetValue(CountdownSettingsKeys.TargetDate, out object? storedTargetDate);
        DateTimeOffset targetDate = CountdownLogic.ReadDateValue(storedTargetDate, fallbackDate);
        if (string.IsNullOrWhiteSpace(targetTimeZoneId))
        {
            targetDate = CountdownTimeZone.MigrateLegacyDateOnlyTarget(targetDate, targetTimeZoneId);
        }

        return new CountdownState(title, targetDate, tileEnabled, toastEnabled, targetTimeZoneId);
    }

    private static void SaveCore(IDictionary<string, object> values, CountdownState state)
    {
        values[CountdownSettingsKeys.Countdowns] = CountdownStateJson.SerializeCountdowns(state.Countdowns);
        values[CountdownSettingsKeys.SelectedCountdownId] = state.SelectedCountdownId;
        if (!state.HasCountdowns)
        {
            values[CountdownSettingsKeys.Title] = string.Empty;
            values.Remove(CountdownSettingsKeys.TargetDate);
            values[CountdownSettingsKeys.TargetTimeZoneId] = CountdownTimeZone.DefaultTimeZoneId;
            values[CountdownSettingsKeys.TileEnabled] = false;
            values[CountdownSettingsKeys.ToastEnabled] = false;
            return;
        }

        values[CountdownSettingsKeys.Title] = state.Title;
        values[CountdownSettingsKeys.TargetDate] = state.TargetDate;
        values[CountdownSettingsKeys.TargetTimeZoneId] = state.TargetTimeZoneId;
        values[CountdownSettingsKeys.TileEnabled] = state.TileEnabled;
        values[CountdownSettingsKeys.ToastEnabled] = state.AnyToastEnabled;
    }

    private static IDisposable EnterLock()
    {
        Mutex mutex = new(false, SettingsMutexName);
        bool lockTaken = false;
        try
        {
            try
            {
                lockTaken = mutex.WaitOne(LockTimeout);
            }
            catch (AbandonedMutexException)
            {
                lockTaken = true;
            }

            if (!lockTaken)
            {
                throw new TimeoutException("Timed out waiting for countdown settings.");
            }

            return new MutexReleaser(mutex);
        }
        catch
        {
            if (!lockTaken)
            {
                mutex.Dispose();
            }

            throw;
        }
    }

    private static bool HasLegacyCountdown(IDictionary<string, object> values, string title)
    {
        return !string.IsNullOrWhiteSpace(title) || values.ContainsKey(CountdownSettingsKeys.TargetDate);
    }

    private static bool ReadBool(IDictionary<string, object> values, string key)
    {
        return values.TryGetValue(key, out object? storedValue) && storedValue is bool value && value;
    }

    private static string? ReadString(IDictionary<string, object> values, string key)
    {
        return values.TryGetValue(key, out object? value) ? value as string : null;
    }

    private sealed class MutexReleaser : IDisposable
    {
        private readonly Mutex _mutex;
        private bool _isDisposed;

        public MutexReleaser(Mutex mutex)
        {
            _mutex = mutex;
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _mutex.ReleaseMutex();
            _mutex.Dispose();
        }
    }
}
