using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using System;

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
        MainWindow window = new();
        _window = window;

        if (IsStartupTaskLaunch())
        {
            await window.DoStartupTaskAsync();
            window.Close();
            return;
        }

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
