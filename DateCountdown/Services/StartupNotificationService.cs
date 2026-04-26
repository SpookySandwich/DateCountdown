using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using System;
using System.Threading.Tasks;
using Windows.ApplicationModel;

namespace DateCountdown.Services;

internal sealed class StartupNotificationService
{
    private const string NotificationTag = "tag";
    private const string StartupTaskId = "DateCountdownStartupId";

    public async Task<bool> EnsureStartupTaskEnabledAsync()
    {
        try
        {
            StartupTask startupTask = await StartupTask.GetAsync(StartupTaskId);
            if (startupTask.State == StartupTaskState.Enabled)
            {
                return true;
            }

            StartupTaskState newState = await startupTask.RequestEnableAsync();
            return newState == StartupTaskState.Enabled || startupTask.State == StartupTaskState.Enabled;
        }
        catch
        {
            return false;
        }
    }

    public async Task DisableStartupTaskAsync()
    {
        try
        {
            StartupTask startupTask = await StartupTask.GetAsync(StartupTaskId);
            if (startupTask.State == StartupTaskState.Enabled)
            {
                startupTask.Disable();
            }
        }
        catch
        {
        }
    }

    public void ShowToast(string title, string content)
    {
        try
        {
            AppNotification notification = new AppNotificationBuilder()
                .AddText(title)
                .AddText(content)
                .BuildNotification();

            notification.Tag = NotificationTag;
            AppNotificationManager.Default.Show(notification);
        }
        catch
        {
        }
    }
}
