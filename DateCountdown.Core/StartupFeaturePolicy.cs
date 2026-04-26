namespace DateCountdown.Core
{
    public sealed class StartupFeaturePolicy
    {
        private readonly bool _supportsLiveTileStartup;

        public StartupFeaturePolicy(bool supportsLiveTileStartup)
        {
            _supportsLiveTileStartup = supportsLiveTileStartup;
        }

        public bool RequiresStartupTask(CountdownState state)
        {
            return state.ToastEnabled || (_supportsLiveTileStartup && state.TileEnabled);
        }

        public CountdownState NormalizeState(CountdownState state)
        {
            return _supportsLiveTileStartup || !state.TileEnabled
                ? state
                : state.With(tileEnabled: false);
        }
    }
}
