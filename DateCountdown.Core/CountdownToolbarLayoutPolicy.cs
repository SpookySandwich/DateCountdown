namespace DateCountdown.Core;

public static class CountdownToolbarLayoutPolicy
{
    public static CountdownToolbarLayout ResolveEffectiveLayout(
        CountdownToolbarLayout layout,
        bool experimentalFeaturesEnabled)
    {
        return experimentalFeaturesEnabled
            ? layout.PromoteToPrimary(CountdownToolbarAction.DesktopOverlay)
            : layout;
    }

    public static bool IsActionVisible(
        CountdownToolbarAction action,
        CountdownState state,
        bool experimentalFeaturesEnabled,
        bool isDesktopOverlayAvailable)
    {
        return action switch
        {
            CountdownToolbarAction.StartupNotification => state.HasCountdowns,
            CountdownToolbarAction.GlanceSurface => state.HasCountdowns,
            CountdownToolbarAction.Delete => state.CanRemoveCountdown,
            CountdownToolbarAction.Edit => state.HasCountdowns,
            CountdownToolbarAction.DesktopOverlay => state.HasCountdowns && experimentalFeaturesEnabled && isDesktopOverlayAvailable,
            _ => true
        };
    }

    public static bool IsActionEnabled(
        CountdownToolbarAction action,
        CountdownState state,
        bool experimentalFeaturesEnabled,
        bool isDesktopOverlayAvailable,
        bool isWindows11OrGreater,
        bool canCommitDraft)
    {
        return action switch
        {
            CountdownToolbarAction.StartupNotification => state.ToastEnabled || canCommitDraft,
            CountdownToolbarAction.GlanceSurface => state.HasCountdowns && (isWindows11OrGreater || state.TileEnabled || canCommitDraft),
            CountdownToolbarAction.Delete => state.CanRemoveCountdown,
            CountdownToolbarAction.Edit => state.HasCountdowns,
            CountdownToolbarAction.Add => true,
            CountdownToolbarAction.DesktopOverlay => state.HasCountdowns && experimentalFeaturesEnabled && isDesktopOverlayAvailable,
            _ => false
        };
    }
}
