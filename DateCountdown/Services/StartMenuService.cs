using System;
using System.Security;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Core;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;
using Windows.UI.StartScreen;

namespace DateCountdown.Services;

internal sealed class StartMenuService
{
    public async Task<bool> IsPinnedAsync()
    {
        try
        {
            AppListEntry? entry = await GetAppListEntryAsync();
            return entry is not null &&
                await StartScreenManager.GetDefault().ContainsAppListEntryAsync(entry);
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> RequestPinAsync()
    {
        try
        {
            AppListEntry? entry = await GetAppListEntryAsync();
            if (entry is not null)
            {
                return await StartScreenManager.GetDefault().RequestAddAppListEntryAsync(entry);
            }
        }
        catch
        {
        }

        return false;
    }

    public void ClearLiveTile()
    {
        try
        {
            TileUpdateManager.CreateTileUpdaterForApplication().Clear();
        }
        catch
        {
        }
    }

    public Task UpdateLiveTileAsync(string title, string content)
    {
        try
        {
            string xml = $"""
                <tile>
                    <visual branding="name">
                        <binding template="TileMedium">
                            <text>{XmlEscape(title)}</text>
                            <text hint-style="captionSubtle">{XmlEscape(content)}</text>
                        </binding>
                        <binding template="TileWide">
                            <text hint-style="title">{XmlEscape(title)}</text>
                            <text hint-style="body">{XmlEscape(content)}</text>
                        </binding>
                    </visual>
                </tile>
                """;

            XmlDocument document = new();
            document.LoadXml(xml);
            TileUpdateManager.CreateTileUpdaterForApplication().Update(new TileNotification(document));
        }
        catch
        {
        }

        return Task.CompletedTask;
    }

    private static async Task<AppListEntry?> GetAppListEntryAsync()
    {
        var entries = await Package.Current.GetAppListEntriesAsync();
        return entries.Count > 0 ? entries[0] : null;
    }

    private static string XmlEscape(string value)
    {
        return SecurityElement.Escape(value) ?? string.Empty;
    }
}
