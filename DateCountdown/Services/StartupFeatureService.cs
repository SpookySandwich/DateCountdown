using DateCountdown.Core;
using System.Threading.Tasks;

namespace DateCountdown.Services;

internal sealed class StartupFeatureService
{
    private readonly StartupFeaturePolicy _policy;
    private readonly StartupNotificationService _startupNotificationService;

    public StartupFeatureService(bool supportsStartTile, StartupNotificationService startupNotificationService)
    {
        _policy = new StartupFeaturePolicy(supportsStartTile);
        _startupNotificationService = startupNotificationService;
    }

    public bool RequiresStartupTask(CountdownState state, CountdownPreferences preferences)
    {
        return _policy.RequiresStartupTask(state, preferences);
    }

    public CountdownState NormalizeState(CountdownState state)
    {
        return _policy.NormalizeState(state);
    }

    public async Task<bool> EnsureAvailableForAsync(CountdownState state, CountdownPreferences preferences)
    {
        return !RequiresStartupTask(state, preferences) ||
            await _startupNotificationService.EnsureStartupTaskEnabledAsync();
    }

    public async Task ReconcileAsync(CountdownState state, CountdownPreferences preferences)
    {
        if (!RequiresStartupTask(state, preferences))
        {
            await _startupNotificationService.DisableStartupTaskAsync();
        }
    }
}
