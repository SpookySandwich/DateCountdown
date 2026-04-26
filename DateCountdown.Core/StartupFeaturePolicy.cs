namespace DateCountdown.Core
{
    public sealed class StartupFeaturePolicy
    {
        private readonly bool _supportsStartTile;

        public StartupFeaturePolicy(bool supportsStartTile)
        {
            _supportsStartTile = supportsStartTile;
        }

        public bool RequiresStartupTask(CountdownState state, CountdownPreferences preferences)
        {
            return preferences.OpenWindowAtStartup ||
                state.AnyToastEnabled ||
                (_supportsStartTile && state.TileEnabled);
        }

        public CountdownState NormalizeState(CountdownState state)
        {
            return _supportsStartTile || !state.TileEnabled
                ? state
                : state.With(tileEnabled: false);
        }
    }
}
