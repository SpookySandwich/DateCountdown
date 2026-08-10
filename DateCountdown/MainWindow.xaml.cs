using DateCountdown.Core;
using DateCountdown.Services;
using DateCountdown.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.Windows.ApplicationModel.Resources;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace DateCountdown;

public sealed partial class MainWindow : Window
{
    private const string TileGlyph = "\uECA5";
    private const string WidgetAddGlyph = "\uF036";
    private const string ApplyStyleTemplateGlyph = "\uE77F";
    private const double MainContentNavigationOffset = 34;
    private const double InlineEditorEntranceOffset = 14;
    private static readonly object ToolbarLayoutDividerTag = new();
    private static readonly string[] PreferredTimeZoneIds =
    [
        CountdownTimeZone.AnywhereOnEarthId,
        CountdownTimeZone.ChinaId,
        "Eastern Standard Time",
        "Central Standard Time",
        "Mountain Standard Time",
        "Pacific Standard Time",
        "Alaskan Standard Time",
        "Hawaiian Standard Time",
        "UTC"
    ];
    private static readonly Duration VisibilityMotionDuration = new(TimeSpan.FromMilliseconds(167));
    private static readonly IReadOnlyList<DesktopCountdownStyleTemplate> DesktopCountdownStyleTemplates =
    [
        new(
            "DesktopCountdown/StyleTemplateSoftGlow/Title",
            "DesktopCountdown/StyleTemplateSoftGlow/Description",
            """
            .countdown {
                color: white;
                font-family: 'Segoe UI Variable Display', 'Segoe UI', sans-serif;
                text-align: center;
                text-shadow: 0 2px 8px rgba(0, 0, 0, 0.78), 0 0 22px rgba(0, 0, 0, 0.42);
            }

            .days {
                font-weight: 300;
                line-height: 1.08;
                white-space: nowrap;
            }

            .title {
                font-weight: 600;
                margin-top: 0.22em;
                opacity: 0.9;
            }
            """),
        new(
            "DesktopCountdown/StyleTemplateMinimalMono/Title",
            "DesktopCountdown/StyleTemplateMinimalMono/Description",
            """
            .countdown {
                color: rgba(255, 255, 255, 0.94);
                font-family: 'Segoe UI Variable Display', 'Segoe UI', sans-serif;
                text-align: right;
                text-shadow: 0 1px 5px rgba(0, 0, 0, 0.72);
            }

            .days {
                font-weight: 300;
                line-height: 1.08;
                white-space: nowrap;
            }

            .title {
                font-weight: 600;
                margin-top: 0.3em;
                letter-spacing: 0.08em;
                opacity: 0.76;
                text-transform: uppercase;
            }
            """),
        new(
            "DesktopCountdown/StyleTemplateWarmAccent/Title",
            "DesktopCountdown/StyleTemplateWarmAccent/Description",
            """
            .countdown {
                color: #fff2c2;
                text-align: left;
                text-shadow: 0 2px 8px rgba(0, 0, 0, 0.86), 0 0 18px rgba(0, 0, 0, 0.52);
            }

            .days {
                font-weight: 600;
                line-height: 1.08;
                white-space: nowrap;
            }

            .title {
                color: #ffd88a;
                letter-spacing: 0.04em;
                margin-top: 0.18em;
                opacity: 0.92;
            }
            """)
    ];

    private readonly bool _systemAnimationsEnabled = new Windows.UI.ViewManagement.UISettings().AnimationsEnabled;
    private readonly bool _isWindows11OrGreater = OperatingSystemInfo.IsWindows11OrGreater();
    private readonly CountdownSettingsStore _settingsStore = new();
    private readonly ResourceLoader _resourceLoader = new();
    private readonly StartMenuService _startMenuService = new();
    private readonly StartupFeatureService _startupFeatureService;
    private readonly StartupNotificationService _startupNotificationService = new();
    private readonly WidgetSyncService _widgetSyncService;
    private readonly WindowChromeService _windowChromeService = new();
    private readonly DesktopCountdownService _desktopCountdownService = new();
    private readonly DispatcherTimer _dateRefreshTimer = new();
    private readonly DesktopTrayService _desktopTrayService;
    private readonly CountdownAutomationServer _automationServer;

    private CountdownState _state = CountdownState.CreateEmpty();
    private CountdownPreferences _preferences = CountdownPreferences.Default;
    private DateTimeOffset _previewTargetDate = DateTimeOffset.Now;
    private IReadOnlyList<CountdownTimeZoneOption> _timeZoneOptions = Array.Empty<CountdownTimeZoneOption>();
    private IReadOnlyList<HourDisplayModeOption> _hourDisplayModeOptions = Array.Empty<HourDisplayModeOption>();
    private double _countdownSelectorDragStartOffset;
    private double _countdownSelectorDragStartX;
    private bool _didDragCountdownSelector;
    private bool _isDraggingCountdownSelector;
    private bool _isAdjustingDesktopCountdown;
    private bool _hasUnsavedOverlayCss;
    private bool _isDesktopOverlayAvailable;
    private bool _isClosing;
    private bool _isExiting;
    private bool _isMotionReady;
    private bool _isDesktopCountdownPageVisible;
    private bool _isEditorVisible;
    private bool _isSettingsVisible;
    private bool _isToolbarLayoutPageVisible;
    private CountdownDisplayLayoutKind _durationLayoutKind = CountdownDisplayLayoutKind.SingleLine;
    private bool _isUpdatingControls;
    private bool _isUpdatingSettingsControls = true;
    private string _editingCountdownId = string.Empty;
    private DateTimeOffset _lastCountdownSelectorDragAt = DateTimeOffset.MinValue;
    private Storyboard? _mainContentOffsetStoryboard;
    private Storyboard? _countdownSurfaceStoryboard;
    private Storyboard? _countdownSelectorVisibilityStoryboard;
    private Storyboard? _inlineEditorVisibilityStoryboard;
    private Flyout? _widgetHelpFlyout;

    private bool AnimationsEnabled => _systemAnimationsEnabled && !_preferences.ReduceMotion;

    private sealed record DesktopCountdownStyleTemplate(string TitleKey, string DescriptionKey, string Css);

    private sealed record ToolbarButtonState(Button Button, bool IsVisible, bool IsEnabled, bool IsActive);

    private sealed record CountdownTimeZoneOption(string Id, string DisplayName);

    private sealed record HourDisplayModeOption(CountdownHourDisplayMode Mode, string DisplayName);

    public MainWindow()
    {
        _widgetSyncService = new WidgetSyncService(_isWindows11OrGreater);
        _startupFeatureService = new StartupFeatureService(!_isWindows11OrGreater, _startupNotificationService);

        InitializeComponent();
        _automationServer = new CountdownAutomationServer(DispatcherQueue, ExecuteAutomationRequestAsync);
        InitializeTimeZoneOptions();
        InitializeHourDisplayModeOptions();
        _isUpdatingSettingsControls = false;

        _windowChromeService.Initialize(this, AppTitleBar, GetString("AppName"), _isWindows11OrGreater);
        _desktopTrayService = new DesktopTrayService(
            this,
            () => HasEnabledDesktopOverlays() && !_isExiting,
            ExitFromTray,
            GetString("Tray/Tooltip"),
            GetString("Tray/Open"),
            GetString("Tray/Exit"));
        _dateRefreshTimer.Tick += DateRefreshTimer_Tick;
        Activated += MainWindow_Activated;
        Closed += MainWindow_Closed;

        LoadPreferences();
        LoadState();
        MigrateLegacyDesktopOverlayPreference();
        ReconcileDesktopOverlaySettingsWithState();
        SetDisplay();
        ApplyMotionPreference();
        _isMotionReady = true;
        ApplyDesktopCountdownPreference();
        ScheduleNextDateRefresh();
        SyncWidgets();
        _ = ReconcileStartupTaskAsync();
        _automationServer.Start();
    }

    private string GetString(string key)
    {
        return _resourceLoader.GetString(key);
    }

    private CountdownDisplayText CreateDisplayText()
    {
        return CountdownDisplayText.FromResources(GetString);
    }

    private string FormatCountdown(DateTimeOffset targetDate)
    {
        return CreateDisplayText().FormatCountdown(
            targetDate,
            DateTimeOffset.Now,
            _preferences,
            CultureInfo.CurrentCulture);
    }

    private CountdownDisplayContent FormatCountdownContent(DateTimeOffset targetDate)
    {
        return CreateDisplayText().FormatCountdownContent(
            targetDate,
            DateTimeOffset.Now,
            _preferences,
            CultureInfo.CurrentCulture);
    }

    private static DateTimeOffset CreateDefaultTargetDate()
    {
        return CountdownTimeZone.CreateDefaultTargetDate(DateTimeOffset.Now);
    }

    private DateTimeOffset GetSelectedOrDefaultTargetDate()
    {
        return _state.SelectedCountdown?.TargetDate ?? CreateDefaultTargetDate();
    }

    private void LoadPreferences()
    {
        _preferences = _settingsStore.LoadPreferences();
    }

    private void SavePreferences(CountdownPreferences preferences)
    {
        _preferences = preferences;
        _settingsStore.SavePreferences(_preferences);
    }

    private void LoadState()
    {
        DateTimeOffset now = DateTimeOffset.Now;
        DateTimeOffset fallbackTargetDate = CountdownTimeZone.CreateDefaultTargetDate(now);
        _state = ApplyPreferencesToState(
            NormalizeStateForCurrentOs(_settingsStore.Load(fallbackTargetDate)),
            now,
            out bool stateChanged);
        if (stateChanged)
        {
            _settingsStore.Save(_state);
        }
    }

    private CountdownState NormalizeStateForCurrentOs(CountdownState state)
    {
        return _startupFeatureService.NormalizeState(state);
    }

    private CountdownState ApplyPreferencesToState(CountdownState state)
    {
        return ApplyPreferencesToState(state, DateTimeOffset.Now, out _);
    }

    private CountdownState ApplyPreferencesToState(CountdownState state, DateTimeOffset now, out bool stateChanged)
    {
        CountdownState nextState = state.WithDefaultTitles(FormatNewCountdownTitle);
        if (_preferences.AutoDeleteExpiredCountdowns)
        {
            nextState = nextState.RemoveExpiredCountdowns(now);
            nextState = nextState.WithDefaultTitles(FormatNewCountdownTitle);
        }

        stateChanged = !ReferenceEquals(nextState, state);
        return _preferences.SortCountdownsByDaysLeft
            ? nextState.SortByDaysLeft(now)
            : nextState;
    }

    private void SetDisplay()
    {
        _isUpdatingControls = true;
        try
        {
            UpdateEditorFromState();
        }
        finally
        {
            _isUpdatingControls = false;
        }

        string toastTooltip = GetString("ToastButton/Tooltip");
        string addTooltip = GetString("AddCountdownButton/Tooltip");
        string editTooltip = GetString("EditCountdownButton/Tooltip");
        string deleteTooltip = GetString("DeleteCountdownButton/Tooltip");
        string cancelTooltip = GetString("Editor/CancelButtonText");
        string saveTooltip = GetString("Editor/SaveButtonText");
        string desktopOverlayTooltip = GetString("DesktopCountdownMenuItem/Text");
        string moreTooltip = GetString("MoreButton/Tooltip");

        ConfigureGlanceSurfaceButton();
        ToolTipService.SetToolTip(NotifyButton, toastTooltip);
        ToolTipService.SetToolTip(AddButton, addTooltip);
        ToolTipService.SetToolTip(EditButton, editTooltip);
        ToolTipService.SetToolTip(DeleteButton, deleteTooltip);
        ToolTipService.SetToolTip(CancelEditorButton, cancelTooltip);
        ToolTipService.SetToolTip(SaveEditorButton, saveTooltip);
        ToolTipService.SetToolTip(DesktopOverlayButton, desktopOverlayTooltip);
        ToolTipService.SetToolTip(MoreButton, moreTooltip);
        AutomationProperties.SetName(NotifyButton, toastTooltip);
        AutomationProperties.SetName(AddButton, addTooltip);
        AutomationProperties.SetName(EditButton, editTooltip);
        AutomationProperties.SetName(DeleteButton, deleteTooltip);
        AutomationProperties.SetName(CancelEditorButton, cancelTooltip);
        AutomationProperties.SetName(SaveEditorButton, saveTooltip);
        AutomationProperties.SetName(DesktopOverlayButton, desktopOverlayTooltip);
        AutomationProperties.SetName(MoreButton, moreTooltip);
        AutomationProperties.SetHelpText(NotifyButton, GetString("ToastButton/HelpText"));
        AutomationProperties.SetHelpText(AddButton, GetString("AddCountdownButton/HelpText"));
        AutomationProperties.SetHelpText(EditButton, GetString("EditCountdownButton/HelpText"));
        AutomationProperties.SetHelpText(DeleteButton, GetString("DeleteCountdownButton/HelpText"));
        AutomationProperties.SetHelpText(CancelEditorButton, GetString("Editor/CancelButtonHelpText"));
        AutomationProperties.SetHelpText(SaveEditorButton, GetString("Editor/SaveButtonHelpText"));
        AutomationProperties.SetHelpText(DesktopOverlayButton, GetString("DesktopCountdownMenuItem/HelpText"));
        AutomationProperties.SetHelpText(MoreButton, GetString("MoreButton/HelpText"));
        AutomationProperties.SetName(CountdownSelectorRail, GetString("CountdownSelector/Name"));
        AutomationProperties.SetName(InlineEditorPanel, GetString("Editor/Title/Edit"));
        AutomationProperties.SetName(CountdownDisplayPanel, GetString("EmptyState/Title"));
        DateEditorHeaderText.Text = GetString("TargetDatePicker/Header");
        TimePickerTargetTime.Header = GetString("TargetTimePicker/Header");
        ComboBoxTargetTimeZone.Header = GetString("TargetTimeZoneComboBox/Header");
        TextBoxTitle.Header = GetString("Title/Header");
        AutomationProperties.SetName(DatePickerTargetDate, GetString("TargetDatePicker/Name"));
        AutomationProperties.SetName(TimePickerTargetTime, GetString("TargetTimePicker/Name"));
        AutomationProperties.SetName(ComboBoxTargetTimeZone, GetString("TargetTimeZoneComboBox/Name"));
        AutomationProperties.SetName(EditorTimeOptionsButton, GetString("TargetTimePicker/Header"));
        AutomationProperties.SetName(EditorTimeZoneOptionsButton, GetString("TargetTimeZoneComboBox/Header"));
        ToolTipService.SetToolTip(EditorTimeOptionsButton, GetString("TargetTimePicker/Header"));
        ToolTipService.SetToolTip(EditorTimeZoneOptionsButton, GetString("TargetTimeZoneComboBox/Header"));
        AutomationProperties.SetName(TextBoxTitle, GetString("Title/PlaceholderText"));

        ConfigureSettingsText();
        RefreshDesktopOverlayAvailability();
        UpdateSettingsControls();
        ApplyDisplaySizePreference();
        UpdateButtonStatus();
    }

    private void DateRefreshTimer_Tick(object? sender, object e)
    {
        _dateRefreshTimer.Stop();
        RefreshDateDependentSurfaces();
        ScheduleNextDateRefresh();
    }

    private void ScheduleNextDateRefresh()
    {
        _dateRefreshTimer.Stop();
        _dateRefreshTimer.Interval = CountdownRefreshSchedule.GetDelayUntilNextCountdownRefresh(
            DateTimeOffset.Now,
            _preferences,
            _state.Countdowns);
        _dateRefreshTimer.Start();
    }

    private void RefreshDateDependentSurfaces()
    {
        bool stateChanged = ApplyDateDependentPreferencesIfNeeded(DateTimeOffset.Now);
        if (stateChanged)
        {
            ReconcileDesktopOverlaySettingsWithState();
            _settingsStore.Save(_state);
            _isUpdatingControls = true;
            try
            {
                UpdateEditorFromState();
            }
            finally
            {
                _isUpdatingControls = false;
            }
        }
        else if (!_isUpdatingControls && _isEditorVisible)
        {
            UpdateEditorPreviewFromControls();
            UpdateEditorCommandButtons();
        }
        else
        {
            UpdateCountdownPreview();
        }

        SyncWidgets();
        if (!_isWindows11OrGreater)
        {
            _ = UpdateLiveTileFromStateAsync();
        }

        UpdateButtonStatus();
    }

    private bool ApplyDateDependentPreferencesIfNeeded(DateTimeOffset now)
    {
        if (!_preferences.AutoDeleteExpiredCountdowns)
        {
            return false;
        }

        CountdownState nextState = _state.RemoveExpiredCountdowns(now);
        if (ReferenceEquals(nextState, _state))
        {
            return false;
        }

        _state = ApplyPreferencesToState(nextState, now, out _);
        return true;
    }

    private string FormatNewCountdownTitle(int number)
    {
        string format = GetString("NewCountdownTitleFormat");
        return string.Format(CultureInfo.CurrentCulture, format, number);
    }

    private async void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.Deactivated)
        {
            return;
        }

        RefreshDesktopOverlayAvailability();
        RefreshDateDependentSurfaces();

        if (_isWindows11OrGreater)
        {
            UpdateGlanceSurfaceStatus();
            return;
        }

        await RefreshStartTileStateAsync();
    }

    private async Task RefreshStartTileStateAsync()
    {
        if (_state.TileEnabled && !await _startMenuService.IsPinnedAsync())
        {
            await ApplyCommittedStateAsync(_state.With(tileEnabled: false), reconcileStartupTask: true);
        }
    }

    private async Task ApplyCommittedStateAsync(
        CountdownState state,
        bool reconcileStartupTask,
        bool refreshEditor = false,
        bool updateButtonStatus = true)
    {
        _state = ApplyPreferencesToState(NormalizeStateForCurrentOs(state), DateTimeOffset.Now, out _);
        ReconcileDesktopOverlaySettingsWithState();
        RootGrid.UpdateLayout();

        if (refreshEditor)
        {
            _isUpdatingControls = true;
            try
            {
                UpdateEditorFromState();
            }
            finally
            {
                _isUpdatingControls = false;
            }
        }
        else
        {
            UpdateCountdownPreview();
            UpdateCountdownSelector();
        }

        _settingsStore.Save(_state);
        SyncWidgets();
        UpdateDesktopCountdown();
        ScheduleNextDateRefresh();

        if (!_isWindows11OrGreater)
        {
            await UpdateLiveTileFromStateAsync();
        }

        if (reconcileStartupTask)
        {
            await ReconcileStartupTaskAsync();
        }

        if (updateButtonStatus)
        {
            UpdateButtonStatus();
        }
    }

    private async Task ReconcileStartupTaskAsync()
    {
        await _startupFeatureService.ReconcileAsync(_state, _preferences);
    }

    private async Task UpdateLiveTileFromStateAsync()
    {
        if (!_state.HasCountdowns || !_state.TileEnabled)
        {
            _startMenuService.ClearLiveTile();
            return;
        }

        CountdownDisplayText displayText = CreateDisplayText();
        string daysLeft = displayText.FormatCountdown(
            _state.TargetDate,
            DateTimeOffset.Now,
            _preferences,
            CultureInfo.CurrentCulture);
        await _startMenuService.UpdateLiveTileAsync(daysLeft, displayText.FormatTitle(_state.Title));
    }

    private void SyncWidgets()
    {
        _widgetSyncService.UpdatePinnedWidgets(_state, DateTimeOffset.Now, CreateDisplayText(), _preferences);
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        _isClosing = true;
        CompleteShutdown();
    }

    private void CompleteShutdown()
    {
        CommitUnsavedOverlayCss();
        StopDateRefreshTimer();
        _automationServer.Dispose();
        _desktopTrayService.Dispose();
        _desktopCountdownService.Dispose();
    }

    private async Task<CountdownAutomationResponse> ExecuteAutomationRequestAsync(CountdownAutomationRequest request)
    {
        if (request.Version != CountdownAutomationRequest.CurrentVersion)
        {
            return new CountdownAutomationResponse(false, "UnsupportedVersion");
        }

        if (request.AddRequest is null)
        {
            return new CountdownAutomationResponse(false, "InvalidRequest");
        }

        CountdownAddResult result = CountdownAddCommand.Execute(_state, request.AddRequest, DateTimeOffset.Now);
        if (!result.IsSuccess)
        {
            return CountdownAutomationResponse.FromResult(result);
        }

        await ApplyCommittedStateAsync(result.State, reconcileStartupTask: false);
        return CountdownAutomationResponse.FromResult(result);
    }

    public void RestoreFromExternalActivation()
    {
        if (_isClosing || _isExiting)
        {
            return;
        }

        _ = _desktopTrayService.TryRestoreWindow();
        Activate();
    }

    private void ExitFromTray()
    {
        _isClosing = true;
        _isExiting = true;
        CompleteShutdown();
        Application.Current.Exit();
    }

    private void StopDateRefreshTimer()
    {
        _dateRefreshTimer.Stop();
        _dateRefreshTimer.Tick -= DateRefreshTimer_Tick;
    }

    private async Task<bool> TryApplyStartupBackedStateAsync(CountdownState nextState)
    {
        if (!await _startupFeatureService.EnsureAvailableForAsync(nextState, _preferences))
        {
            _startupNotificationService.ShowStatusToast(
                GetString("CreateStartupTaskFailedNotification/Title"),
                GetString("CreateStartupTaskFailedNotification/Content"));
            UpdateButtonStatus();
            return false;
        }

        await ApplyCommittedStateAsync(nextState, reconcileStartupTask: false);
        return true;
    }
}
