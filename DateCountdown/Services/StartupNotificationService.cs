using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel;

namespace DateCountdown.Services;

internal sealed class StartupNotificationService
{
    private const string StatusNotificationTag = "status";
    private const string StartupNotificationGroup = "startup";
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

    public void ShowStatusToast(string title, string content)
    {
        ShowToast(title, content, StatusNotificationTag, group: string.Empty);
    }

    public void ShowStartupCountdownToast(string countdownId, string title, string content)
    {
        ShowToast(title, content, CreateCountdownNotificationTag(countdownId), StartupNotificationGroup);
    }

    private static string CreateCountdownNotificationTag(string countdownId)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(countdownId));
        return Convert.ToHexString(hash, 0, 8);
    }

    private static void ShowToast(string title, string content, string tag, string group)
    {
        try
        {
            AppNotification notification = new AppNotificationBuilder()
                .AddText(title)
                .AddText(content)
                .BuildNotification();

            notification.Tag = tag;
            notification.Group = group;
            AppNotificationManager.Default.Show(notification);
        }
        catch
        {
        }
    }
}
