using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using DateCountdown.Services;

namespace DateCountdown;

public partial class App : Application
{
    private const string SingleInstanceKey = "DateCountdown.MainInstance";

    private Window? _window;
    private AppInstance? _mainInstance;
    private bool _hasPendingActivation;

    public App()
    {
        InitializeComponent();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        AppActivationArguments activationArguments = AppInstance.GetCurrent().GetActivatedEventArgs();
        if (IsStartupTaskLaunch(activationArguments) &&
            !await RunStartupTaskAsync())
        {
            Exit();
            return;
        }

        if (await TryRedirectActivationToMainInstanceAsync(activationArguments))
        {
            Exit();
            return;
        }

        MainWindow window = OpenMainWindow();
        if (_hasPendingActivation)
        {
            _hasPendingActivation = false;
            window.RestoreFromExternalActivation();
        }
    }

    private async Task<bool> TryRedirectActivationToMainInstanceAsync(AppActivationArguments activationArguments)
    {
        AppInstance mainInstance = AppInstance.FindOrRegisterForKey(SingleInstanceKey);
        if (mainInstance.IsCurrent)
        {
            _mainInstance = mainInstance;
            _mainInstance.Activated += MainInstance_Activated;
            return false;
        }

        await mainInstance.RedirectActivationToAsync(activationArguments);
        return true;
    }

    private void MainInstance_Activated(object? sender, AppActivationArguments args)
    {
        if (_window is MainWindow window)
        {
            _ = window.DispatcherQueue.TryEnqueue(window.RestoreFromExternalActivation);
            return;
        }

        _hasPendingActivation = true;
    }

    private MainWindow OpenMainWindow()
    {
        MainWindow window = new();
        _window = window;
        window.Closed += MainWindow_Closed;
        window.Activate();
        return window;
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        ((MainWindow)sender).Closed -= MainWindow_Closed;
        _window = null;
        if (_mainInstance is not null)
        {
            _mainInstance.Activated -= MainInstance_Activated;
            _mainInstance.UnregisterKey();
            _mainInstance = null;
        }
    }

    private static async Task<bool> RunStartupTaskAsync()
    {
        try
        {
            return await new StartupTaskRunner(OperatingSystemInfo.IsWindows11OrGreater()).RunAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Startup task failed: {ex}");
            return false;
        }
    }

    private static bool IsStartupTaskLaunch(AppActivationArguments activationArguments)
    {
        return string.Equals(activationArguments.Kind.ToString(), "StartupTask", StringComparison.OrdinalIgnoreCase);
    }
}
