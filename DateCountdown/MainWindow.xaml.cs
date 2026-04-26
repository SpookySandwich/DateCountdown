using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Windows.ApplicationModel.Resources;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using System;
using System.Globalization;
using System.Security;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Core;
using Windows.Data.Xml.Dom;
using Windows.Storage;
using Windows.UI.Notifications;

namespace DateCountdown;

public sealed partial class MainWindow : Window
{
    private const string NotificationTag = "tag";
    private const string StartupTaskId = "DateCountdownStartupId";

    private readonly ApplicationDataContainer _localSettings = ApplicationData.Current.LocalSettings;
    private readonly ResourceLoader _resourceLoader = new();

    private int _dateDifference;
    private bool _newTileEnabled;
    private bool _newToastEnabled;
    private DateTimeOffset _targetDate;
    private bool _tileEnabled;
    private string _title = string.Empty;
    private bool _toastEnabled;

    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        Title = GetString("AppName");
        AppWindow.SetIcon("Assets/AppIcon.ico");

        LoadData();
        SetDisplay();
    }

    internal async Task DoStartupTaskAsync()
    {
        LoadData();
        _dateDifference = CalculateDateDifference(_targetDate);

        if (_toastEnabled)
        {
            ShowToast(FormatString("DaysLeft/Text", _dateDifference.ToString(CultureInfo.CurrentCulture)), _title);
        }

        if (_tileEnabled)
        {
            await UpdateTileAsync(FormatString("DaysLeft/Text", _dateDifference.ToString(CultureInfo.CurrentCulture)), _title);
        }
    }

    private static int CalculateDateDifference(DateTimeOffset targetDate)
    {
        return (int)(targetDate.Date - DateTimeOffset.Now.Date).TotalDays;
    }

    private static DateTimeOffset ReadDateValue(object? value)
    {
        return value switch
        {
            DateTimeOffset date => date,
            DateTime date => new DateTimeOffset(date),
            string text when DateTimeOffset.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out DateTimeOffset date) => date,
            _ => DateTimeOffset.Now,
        };
    }

    private static string XmlEscape(string value)
    {
        return SecurityElement.Escape(value) ?? string.Empty;
    }

    private string GetString(string key)
    {
        return _resourceLoader.GetString(key);
    }

    private string FormatString(string key, string value)
    {
        return string.Format(CultureInfo.CurrentCulture, GetString(key), value);
    }

    private void LoadData()
    {
        _title = _localSettings.Values["title"] as string ?? string.Empty;
        _targetDate = ReadDateValue(_localSettings.Values["targetDate"]);
        _tileEnabled = _localSettings.Values["tileEnabled"] as bool? ?? false;
        _toastEnabled = _localSettings.Values["toastEnabled"] as bool? ?? false;

        _newTileEnabled = _tileEnabled;
        _newToastEnabled = _toastEnabled;
    }

    private void StoreData()
    {
        _localSettings.Values["title"] = _title;
        _localSettings.Values["targetDate"] = _targetDate;
        _localSettings.Values["tileEnabled"] = _tileEnabled;
        _localSettings.Values["toastEnabled"] = _toastEnabled;
    }

    private void SetDisplay()
    {
        _dateDifference = CalculateDateDifference(_targetDate);

        TextBlockTitle.Text = _title;
        TextBlockDays.Text = FormatString("DaysLeft/Text", _dateDifference.ToString(CultureInfo.CurrentCulture));
        TextBoxTitle.Text = _title;
        DatePickerTargetDate.MinDate = DateTimeOffset.Now.Date;
        DatePickerTargetDate.Date = _targetDate;

        string tileTooltip = GetString("TileButton/Tooltip");
        string toastTooltip = GetString("ToastButton/Tooltip");

        ToolTipService.SetToolTip(TileButton, tileTooltip);
        ToolTipService.SetToolTip(NotifyButton, toastTooltip);
        AutomationProperties.SetName(TileButton, tileTooltip);
        AutomationProperties.SetName(NotifyButton, toastTooltip);
        AutomationProperties.SetName(DatePickerTargetDate, GetString("TargetDatePicker/Name"));
        AutomationProperties.SetName(TextBoxTitle, GetString("Title/PlaceholderText"));
        AutomationProperties.SetName(ButtonSet, GetString("ButtonSet/Content"));

        UpdateButtonStatus();
    }

    private void UpdateButtonStatus()
    {
        DateTimeOffset? selectedDate = DatePickerTargetDate.Date;
        string draftTitle = TextBoxTitle.Text ?? string.Empty;
        bool hasChanges =
            selectedDate.HasValue &&
            (selectedDate.Value.Date != _targetDate.Date ||
            draftTitle != _title ||
            _tileEnabled != _newTileEnabled ||
            _toastEnabled != _newToastEnabled);

        ButtonSet.IsEnabled =
            hasChanges &&
            selectedDate.HasValue &&
            selectedDate.Value.Date >= DateTimeOffset.Now.Date &&
            !string.IsNullOrWhiteSpace(draftTitle);

        NotifyButton.Foreground = GetToggleBrush(_newToastEnabled);
        TileButton.Foreground = GetToggleBrush(_newTileEnabled);
    }

    private Brush GetToggleBrush(bool isEnabled)
    {
        string resourceKey = isEnabled ? "TextFillColorPrimaryBrush" : "TextFillColorDisabledBrush";

        if (Application.Current.Resources.TryGetValue(resourceKey, out object resource) && resource is Brush brush)
        {
            return brush;
        }

        return new SolidColorBrush(isEnabled ? Colors.Black : Colors.Gray);
    }

    private async Task<bool> EnsureStartupTaskEnabledAsync()
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

    private async Task<bool> CheckTileExistsAsync()
    {
        try
        {
            AppListEntry entry = (await Package.Current.GetAppListEntriesAsync())[0];
            return await Windows.UI.StartScreen.StartScreenManager.GetDefault().ContainsAppListEntryAsync(entry);
        }
        catch
        {
            return false;
        }
    }

    private async Task PinTileAsync()
    {
        try
        {
            AppListEntry entry = (await Package.Current.GetAppListEntriesAsync())[0];
            await Windows.UI.StartScreen.StartScreenManager.GetDefault().RequestAddAppListEntryAsync(entry);
        }
        catch
        {
        }
    }

    private static void ClearTile()
    {
        try
        {
            TileUpdateManager.CreateTileUpdaterForApplication().Clear();
        }
        catch
        {
        }
    }

    private async Task UpdateTileAsync(string title, string content)
    {
        if (!await CheckTileExistsAsync())
        {
            await PinTileAsync();
        }

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
    }

    private static void ShowToast(string title, string content)
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

    private async void ButtonSet_Click(object sender, RoutedEventArgs e)
    {
        _targetDate = DatePickerTargetDate.Date ?? DateTimeOffset.Now;
        _title = TextBoxTitle.Text ?? string.Empty;
        _tileEnabled = _newTileEnabled;
        _toastEnabled = _newToastEnabled;
        SetDisplay();
        StoreData();

        bool startupEnabled = (!_tileEnabled && !_toastEnabled) || await EnsureStartupTaskEnabledAsync();
        string daysLeft = FormatString("DaysLeft/Text", _dateDifference.ToString(CultureInfo.CurrentCulture));

        if (_tileEnabled && startupEnabled)
        {
            await UpdateTileAsync(daysLeft, _title);
        }
        else
        {
            ClearTile();
        }

        if (_toastEnabled && startupEnabled)
        {
            ShowToast(GetString("SuccessNotification/Title"), FormatString("SuccessNotification/Content", _title));
        }
        else if (_toastEnabled)
        {
            _newToastEnabled = false;
            _toastEnabled = false;
            StoreData();
            UpdateButtonStatus();
            ShowToast(GetString("CreateStartupTaskFailedNotification/Title"), GetString("CreateStartupTaskFailedNotification/Content"));
        }
    }

    private void TextBoxTitle_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateButtonStatus();
    }

    private void DatePickerTargetDate_SelectedDateChanged(CalendarDatePicker sender, CalendarDatePickerDateChangedEventArgs args)
    {
        UpdateButtonStatus();
    }

    private async void TileButton_Click(object sender, RoutedEventArgs e)
    {
        if (!await CheckTileExistsAsync())
        {
            await PinTileAsync();
        }

        _newTileEnabled = !_newTileEnabled;
        UpdateButtonStatus();
    }

    private async void NotifyButton_Click(object sender, RoutedEventArgs e)
    {
        if (!await EnsureStartupTaskEnabledAsync())
        {
            ShowToast(GetString("CreateStartupTaskFailedNotification/Title"), GetString("CreateStartupTaskFailedNotification/Content"));
            _newToastEnabled = false;
            _toastEnabled = false;
        }
        else
        {
            _newToastEnabled = !_newToastEnabled;
        }

        UpdateButtonStatus();
    }
}
