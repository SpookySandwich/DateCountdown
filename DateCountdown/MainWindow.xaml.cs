using DateCountdown.Core;
using DateCountdown.Services;
using DateCountdown.Windowing;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Windows.ApplicationModel.Resources;
using System;
using System.Globalization;
using System.Threading.Tasks;

namespace DateCountdown;

public sealed partial class MainWindow : Window
{
    private const string TileGlyph = "\uECA5";
    private const string StartGlyph = "\uE8FC";

    private readonly bool _isWindows11OrGreater = OperatingSystemInfo.IsWindows11OrGreater();
    private readonly CountdownSettingsStore _settingsStore = new();
    private readonly ResourceLoader _resourceLoader = new();
    private readonly StartMenuService _startMenuService = new();
    private readonly StartupFeatureService _startupFeatureService;
    private readonly StartupNotificationService _startupNotificationService = new();
    private readonly WidgetSyncService _widgetSyncService;
    private readonly WindowChromeService _windowChromeService = new();

    private CountdownState _state = CountdownState.CreateDefault(DateTimeOffset.Now);
    private int _dateDifference;
    private bool _isUpdatingControls;
    private int _startPinStatusLoopVersion;
    private int _startPinStatusRequestVersion;

    public MainWindow()
    {
        _widgetSyncService = new WidgetSyncService(_isWindows11OrGreater);
        _startupFeatureService = new StartupFeatureService(!_isWindows11OrGreater, _startupNotificationService);

        InitializeComponent();

        _windowChromeService.Initialize(this, AppTitleBar, GetString("AppName"), _isWindows11OrGreater);
        Activated += MainWindow_Activated;

        LoadState();
        SetDisplay();
        SyncWidgets();
        _ = ReconcileStartupTaskAsync();
    }

    internal async Task DoStartupTaskAsync()
    {
        LoadState();
        _dateDifference = CountdownLogic.CalculateDaysLeft(_state.TargetDate, DateTimeOffset.Now);

        string daysLeft = FormatDaysLeft(_dateDifference);
        if (_state.ToastEnabled)
        {
            _startupNotificationService.ShowToast(daysLeft, _state.Title);
        }

        if (_state.TileEnabled && !_isWindows11OrGreater)
        {
            await _startMenuService.UpdateLiveTileAsync(daysLeft, _state.Title);
        }
    }

    private string GetString(string key)
    {
        return _resourceLoader.GetString(key);
    }

    private CountdownDisplayText CreateDisplayText()
    {
        return new CountdownDisplayText(
            GetString("AppName"),
            GetString("DaysLeftOneText"),
            GetString("DaysLeftManyText"));
    }

    private string FormatDaysLeft(int daysLeft)
    {
        return CreateDisplayText().FormatDaysLeft(daysLeft, CultureInfo.CurrentCulture);
    }

    private void LoadState()
    {
        _state = NormalizeStateForCurrentOs(_settingsStore.Load(DateTimeOffset.Now));
    }

    private CountdownState NormalizeStateForCurrentOs(CountdownState state)
    {
        return _startupFeatureService.NormalizeState(state);
    }

    private void SetDisplay()
    {
        _isUpdatingControls = true;
        try
        {
            UpdateCountdownPreview();
            TextBoxTitle.PlaceholderText = GetString("Title/PlaceholderText");
            TextBoxTitle.Text = _state.Title;
            DatePickerTargetDate.MinDate = DateTimeOffset.Now.Date;
            DatePickerTargetDate.Date = _state.TargetDate;
        }
        finally
        {
            _isUpdatingControls = false;
        }

        string toastTooltip = GetString("ToastButton/Tooltip");

        ConfigureGlanceSurfaceButton();
        ToolTipService.SetToolTip(NotifyButton, toastTooltip);
        AutomationProperties.SetName(NotifyButton, toastTooltip);
        AutomationProperties.SetHelpText(NotifyButton, GetString("ToastButton/HelpText"));
        AutomationProperties.SetName(DatePickerTargetDate, GetString("TargetDatePicker/Name"));
        AutomationProperties.SetName(TextBoxTitle, GetString("Title/PlaceholderText"));

        UpdateButtonStatus();
    }

    private void UpdateCountdownPreview()
    {
        _dateDifference = CountdownLogic.CalculateDaysLeft(_state.TargetDate, DateTimeOffset.Now);
        TextBlockTitle.Text = _state.Title;
        TextBlockDays.Text = FormatDaysLeft(_dateDifference);
    }

    private void UpdateCountdownPreview(CountdownDraft draft)
    {
        DateTimeOffset targetDate = draft.TargetDate ?? _state.TargetDate;
        _dateDifference = CountdownLogic.CalculateDaysLeft(targetDate, DateTimeOffset.Now);
        TextBlockTitle.Text = draft.Title;
        TextBlockDays.Text = FormatDaysLeft(_dateDifference);
    }

    private void TextBlockDays_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        TextBlockDays.FontSize = e.NewSize.Width switch
        {
            < 320 => 60,
            < 370 => 66,
            _ => 72
        };
    }

    private void UpdateButtonStatus()
    {
        bool canCommitDraft = CreateDraftFromControls().CanCommit(DateTimeOffset.Now);
        NotifyButton.IsEnabled = _state.ToastEnabled || canCommitDraft;
        TileButton.IsEnabled = _isWindows11OrGreater || _state.TileEnabled || canCommitDraft;
        NotifyButton.Foreground = GetToggleBrush(_state.ToastEnabled);
        UpdateGlanceSurfaceStatus();
    }

    private Brush GetToggleBrush(bool isEnabled)
    {
        string resourceKey = isEnabled ? "TextFillColorPrimaryBrush" : "TextFillColorDisabledBrush";

        return GetResourceBrush(resourceKey, new SolidColorBrush(isEnabled ? Colors.White : Colors.Gray));
    }

    private Brush GetResourceBrush(string resourceKey, Brush fallback)
    {
        if (Application.Current.Resources.TryGetValue(resourceKey, out object resource) && resource is Brush brush)
        {
            return brush;
        }

        return fallback;
    }

    private void ConfigureGlanceSurfaceButton()
    {
        GlanceSurfaceIcon.FontFamily = new FontFamily(_isWindows11OrGreater ? "Segoe Fluent Icons, Segoe MDL2 Assets" : "Segoe MDL2 Assets");
        GlanceSurfaceIcon.Glyph = _isWindows11OrGreater ? StartGlyph : TileGlyph;
    }

    private void UpdateGlanceSurfaceStatus()
    {
        if (!_isWindows11OrGreater)
        {
            TileButton.Foreground = GetToggleBrush(_state.TileEnabled);
            ToolTipService.SetToolTip(TileButton, GetString("TileButton/Tooltip"));
            AutomationProperties.SetName(TileButton, GetString("TileButton/Tooltip"));
            AutomationProperties.SetHelpText(TileButton, GetString("TileButton/HelpText"));
            return;
        }

        _ = UpdateStartPinStatusAsync();
    }

    private async Task UpdateStartPinStatusAsync()
    {
        int version = ++_startPinStatusRequestVersion;
        bool isPinned = await _startMenuService.IsPinnedAsync();

        if (version != _startPinStatusRequestVersion)
        {
            return;
        }

        string tooltipKey = isPinned ? "StartPinButton/PinnedTooltip" : "StartPinButton/PinTooltip";
        TileButton.Foreground = GetToggleBrush(isPinned);
        ToolTipService.SetToolTip(TileButton, GetString(tooltipKey));
        AutomationProperties.SetName(TileButton, GetString(tooltipKey));
        AutomationProperties.SetHelpText(TileButton, GetString("StartPinButton/HelpText"));
    }

    private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        if (_isWindows11OrGreater && args.WindowActivationState != WindowActivationState.Deactivated)
        {
            SyncWidgets();
            UpdateGlanceSurfaceStatus();
        }
    }

    private async Task SaveCountdownAsync()
    {
        CountdownDraft draft = CreateDraftFromControls();
        UpdateCountdownPreview(draft);

        if (!draft.TryCommit(DateTimeOffset.Now, out CountdownState? nextState) || nextState is null)
        {
            UpdateButtonStatus();
            return;
        }

        await ApplyCommittedStateAsync(nextState, reconcileStartupTask: false);
    }

    private async Task ApplyCommittedStateAsync(CountdownState state, bool reconcileStartupTask)
    {
        _state = NormalizeStateForCurrentOs(state);
        UpdateCountdownPreview();
        _settingsStore.Save(_state);
        SyncWidgets();

        if (!_isWindows11OrGreater)
        {
            await UpdateLiveTileFromStateAsync();
        }

        if (reconcileStartupTask)
        {
            await ReconcileStartupTaskAsync();
        }

        UpdateButtonStatus();
    }

    private CountdownDraft CreateDraftFromControls(bool? tileEnabled = null, bool? toastEnabled = null)
    {
        return new CountdownDraft(
            TextBoxTitle.Text ?? string.Empty,
            DatePickerTargetDate.Date,
            _isWindows11OrGreater ? false : tileEnabled ?? _state.TileEnabled,
            toastEnabled ?? _state.ToastEnabled);
    }

    private async Task ReconcileStartupTaskAsync()
    {
        await _startupFeatureService.ReconcileAsync(_state);
    }

    private async Task UpdateLiveTileFromStateAsync()
    {
        string daysLeft = FormatDaysLeft(_dateDifference);
        if (_state.TileEnabled)
        {
            await _startMenuService.UpdateLiveTileAsync(daysLeft, _state.Title);
        }
        else
        {
            _startMenuService.ClearLiveTile();
        }
    }

    private void SyncWidgets()
    {
        _widgetSyncService.UpdatePinnedWidgets(_state, DateTimeOffset.Now, CreateDisplayText());
    }

    private async void TextBoxTitle_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_isUpdatingControls)
        {
            await SaveCountdownAsync();
        }
    }

    private async void DatePickerTargetDate_SelectedDateChanged(CalendarDatePicker sender, CalendarDatePickerDateChangedEventArgs args)
    {
        if (!_isUpdatingControls)
        {
            await SaveCountdownAsync();
        }
    }

    private async void TileButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isWindows11OrGreater)
        {
            await _startMenuService.RequestPinAsync();
            StartPinStatusRefreshLoop();
            return;
        }

        if (!await _startMenuService.IsPinnedAsync())
        {
            await _startMenuService.RequestPinAsync();
        }

        bool tileEnabled = !_state.TileEnabled;
        if (!tileEnabled)
        {
            await ApplyCommittedStateAsync(_state.With(tileEnabled: false), reconcileStartupTask: true);
            return;
        }

        CountdownDraft draft = CreateDraftFromControls(tileEnabled: true);
        if (!draft.TryCommit(DateTimeOffset.Now, out CountdownState? nextState) || nextState is null)
        {
            UpdateCountdownPreview(draft);
            UpdateButtonStatus();
            return;
        }

        if (!await _startupFeatureService.EnsureAvailableForAsync(nextState))
        {
            _state = _state.With(tileEnabled: false);
            _settingsStore.Save(_state);
            UpdateButtonStatus();
            _startupNotificationService.ShowToast(
                GetString("CreateStartupTaskFailedNotification/Title"),
                GetString("CreateStartupTaskFailedNotification/Content"));
            return;
        }

        await ApplyCommittedStateAsync(nextState, reconcileStartupTask: false);
    }

    private async void StartPinStatusRefreshLoop()
    {
        int version = ++_startPinStatusLoopVersion;

        for (int i = 0; i < 20; i++)
        {
            if (i > 0)
            {
                await Task.Delay(750);
            }

            if (version != _startPinStatusLoopVersion)
            {
                return;
            }

            await UpdateStartPinStatusAsync();
        }
    }

    private async void NotifyButton_Click(object sender, RoutedEventArgs e)
    {
        bool toastEnabled = !_state.ToastEnabled;
        if (!toastEnabled)
        {
            await ApplyCommittedStateAsync(_state.With(toastEnabled: false), reconcileStartupTask: true);
            return;
        }

        CountdownDraft draft = CreateDraftFromControls(toastEnabled: true);
        if (!draft.TryCommit(DateTimeOffset.Now, out CountdownState? nextState) || nextState is null)
        {
            UpdateCountdownPreview(draft);
            UpdateButtonStatus();
            return;
        }

        if (!await _startupFeatureService.EnsureAvailableForAsync(nextState))
        {
            _startupNotificationService.ShowToast(
                GetString("CreateStartupTaskFailedNotification/Title"),
                GetString("CreateStartupTaskFailedNotification/Content"));
            UpdateButtonStatus();
            return;
        }

        await ApplyCommittedStateAsync(nextState, reconcileStartupTask: false);
    }
}
