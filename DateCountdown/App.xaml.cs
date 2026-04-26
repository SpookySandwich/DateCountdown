using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using System;
using DateCountdown.Services;

namespace DateCountdown;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        if (IsStartupTaskLaunch())
        {
            bool openWindow = false;
            try
            {
                openWindow = await new StartupTaskRunner(OperatingSystemInfo.IsWindows11OrGreater()).RunAsync();
            }
            catch
            {
            }

            if (!openWindow)
            {
                Exit();
                return;
            }
        }

        MainWindow window = new();
        _window = window;
        window.Activate();
    }

    private static bool IsStartupTaskLaunch()
    {
        try
        {
            AppActivationArguments activationArguments = AppInstance.GetCurrent().GetActivatedEventArgs();
            return string.Equals(activationArguments.Kind.ToString(), "StartupTask", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
