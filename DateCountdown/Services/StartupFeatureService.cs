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

    public bool RequiresStartupTask(CountdownState state)
    {
        return _policy.RequiresStartupTask(state);
    }

    public CountdownState NormalizeState(CountdownState state)
    {
        return _policy.NormalizeState(state);
    }

    public async Task<bool> EnsureAvailableForAsync(CountdownState state)
    {
        return !RequiresStartupTask(state) ||
            await _startupNotificationService.EnsureStartupTaskEnabledAsync();
    }

    public async Task ReconcileAsync(CountdownState state)
    {
        if (!RequiresStartupTask(state))
        {
            await _startupNotificationService.DisableStartupTaskAsync();
        }
    }
}
