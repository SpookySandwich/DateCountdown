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
}
