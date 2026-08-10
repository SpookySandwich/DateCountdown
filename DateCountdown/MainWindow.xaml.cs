using DateCountdown.Core;
using DateCountdown.Services;
using DateCountdown.Windowing;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.Windows.ApplicationModel.Resources;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.System;

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
    private bool _isDesktopOverlayAvailable;
    private bool _isExiting;
    private bool _isMotionReady;
    private bool _isDesktopCountdownPageVisible;
    private bool _isEditorVisible;
    private bool _isSettingsVisible;
    private bool _isToolbarLayoutPageVisible;
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
        ReconcileDesktopOverlaySettingsWithState(saveIfChanged: true);
        SetDisplay();
        ApplyMotionPreference();
        _isMotionReady = true;
        ApplyDesktopCountdownPreference();
        ScheduleNextDateRefresh();
        SyncWidgets();
        _ = ReconcileStartupTaskAsync();
    }

    private string GetString(string key)
    {
        return _resourceLoader.GetString(key);
    }

    private void InitializeTimeZoneOptions()
    {
        List<CountdownTimeZoneOption> options = new();
        string localTimeZoneId = CountdownTimeZone.DefaultTimeZoneId;
        AddTimeZoneOption(options, localTimeZoneId, string.Format(
            CultureInfo.CurrentCulture,
            GetString("TimeZone/LocalFormat"),
            CountdownTimeZone.FormatUtcOffset(TimeZoneInfo.Local.GetUtcOffset(DateTimeOffset.Now))));

        foreach (string timeZoneId in PreferredTimeZoneIds)
        {
            AddTimeZoneOption(options, timeZoneId, FormatTimeZoneOption(timeZoneId));
        }

        _timeZoneOptions = options;
        ComboBoxTargetTimeZone.ItemsSource = _timeZoneOptions;
        ComboBoxTargetTimeZone.DisplayMemberPath = nameof(CountdownTimeZoneOption.DisplayName);
    }

    private void InitializeHourDisplayModeOptions()
    {
        _hourDisplayModeOptions =
        [
            new HourDisplayModeOption(CountdownHourDisplayMode.DaysOnly, GetString("HourDisplayMode/DaysOnly")),
            new HourDisplayModeOption(CountdownHourDisplayMode.NearDeadline, GetString("HourDisplayMode/NearDeadline")),
            new HourDisplayModeOption(CountdownHourDisplayMode.HoursOnly, GetString("HourDisplayMode/HoursOnly"))
        ];

        HourDisplayModeComboBox.ItemsSource = _hourDisplayModeOptions;
        HourDisplayModeComboBox.DisplayMemberPath = nameof(HourDisplayModeOption.DisplayName);
    }

    private static void AddTimeZoneOption(
        ICollection<CountdownTimeZoneOption> options,
        string timeZoneId,
        string displayName)
    {
        string normalizedId = CountdownTimeZone.NormalizeTimeZoneId(timeZoneId);
        if (options.Any(option => string.Equals(option.Id, normalizedId, StringComparison.Ordinal)))
        {
            return;
        }

        options.Add(new CountdownTimeZoneOption(normalizedId, displayName));
    }

    private string FormatTimeZoneOption(string timeZoneId)
    {
        TimeZoneInfo timeZone = CountdownTimeZone.FindTimeZoneOrLocal(timeZoneId);
        string resourceKey = timeZoneId switch
        {
            CountdownTimeZone.AnywhereOnEarthId => "TimeZone/AnywhereOnEarth",
            CountdownTimeZone.ChinaId => "TimeZone/China",
            "Eastern Standard Time" => "TimeZone/USEastern",
            "Central Standard Time" => "TimeZone/USCentral",
            "Mountain Standard Time" => "TimeZone/USMountain",
            "Pacific Standard Time" => "TimeZone/USPacific",
            "Alaskan Standard Time" => "TimeZone/Alaska",
            "Hawaiian Standard Time" => "TimeZone/Hawaii",
            "UTC" => "TimeZone/UTC",
            _ => string.Empty
        };

        string label = string.IsNullOrWhiteSpace(resourceKey)
            ? timeZone.DisplayName
            : GetString(resourceKey);
        return string.Format(
            CultureInfo.CurrentCulture,
            GetString("TimeZone/OptionFormat"),
            label,
            CountdownTimeZone.FormatUtcOffset(timeZone.GetUtcOffset(DateTimeOffset.Now)));
    }

    private CountdownDisplayText CreateDisplayText()
    {
        return new CountdownDisplayText(
            GetString("AppName"),
            GetString("DaysLeftOneText"),
            GetString("DaysLeftManyText"),
            GetString("DueTodayText"),
            GetString("HoursLeftOneText"),
            GetString("HoursLeftManyText"),
            GetString("DaysOverdueOneText"),
            GetString("DaysOverdueManyText"),
            GetString("DaysHoursLeftOneDayOneHourText"),
            GetString("DaysHoursLeftOneDayManyHoursText"),
            GetString("DaysHoursLeftManyDaysOneHourText"),
            GetString("DaysHoursLeftManyDaysManyHoursText"));
    }

    private string FormatCountdown(DateTimeOffset targetDate)
    {
        return CreateDisplayText().FormatCountdown(
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

    private void MigrateLegacyDesktopOverlayPreference()
    {
        if (!_state.HasCountdowns || !_preferences.ShowDesktopCountdown || _preferences.DesktopOverlays.Count > 0)
        {
            return;
        }

        DesktopOverlaySettings overlay = new(
            _state.SelectedCountdownId,
            isEnabled: true,
            _preferences.DesktopCountdownX,
            _preferences.DesktopCountdownY,
            _preferences.DesktopCountdownScale,
            _preferences.DesktopCountdownCss);
        _preferences = _preferences.With(
            showDesktopCountdown: false,
            desktopOverlays: new[] { overlay });
        _settingsStore.SavePreferences(_preferences);
    }

    private void ReconcileDesktopOverlaySettingsWithState(bool saveIfChanged)
    {
        HashSet<string> countdownIds = new(_state.Countdowns.Select(countdown => countdown.Id), StringComparer.Ordinal);
        List<DesktopOverlaySettings> overlays = _preferences.DesktopOverlays
            .Where(overlay => countdownIds.Contains(overlay.CountdownId))
            .ToList();

        if (overlays.Count == _preferences.DesktopOverlays.Count)
        {
            return;
        }

        _preferences = _preferences.With(desktopOverlays: overlays);
        if (saveIfChanged)
        {
            _settingsStore.SavePreferences(_preferences);
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

    private void ConfigureSettingsText()
    {
        AutomationProperties.SetName(SettingsBackButton, GetString("Settings/BackButton"));
        ToolTipService.SetToolTip(SettingsBackButton, GetString("Settings/BackButton"));
        AutomationProperties.SetName(ToolbarLayoutBackButton, GetString("Settings/BackButton"));
        ToolTipService.SetToolTip(ToolbarLayoutBackButton, GetString("Settings/BackButton"));
        SettingsTitleText.Text = GetString("Settings/Title");
        ToolbarLayoutPageTitleText.Text = GetString("Settings/ToolbarLayoutPageTitle");
        ToolbarLayoutPageDescriptionText.Text = GetString("Settings/ToolbarLayoutDescription");
        SettingsCountdownsHeaderText.Text = GetString("Settings/CountdownsHeader");
        SettingsAppearanceHeaderText.Text = GetString("Settings/AppearanceHeader");
        SettingsStartupHeaderText.Text = GetString("Settings/StartupHeader");
        SettingsExperimentalHeaderText.Text = GetString("Settings/ExperimentalHeader");
        SettingsAboutHeaderText.Text = GetString("Settings/AboutHeader");

        SettingsMenuItem.Text = GetString("SettingsMenuItem/Text");
        DesktopCountdownMenuItem.Text = GetString("DesktopCountdownMenuItem/Text");
        NotifyMenuItem.Text = GetToolbarActionText(CountdownToolbarAction.StartupNotification);
        TileMenuItem.Text = GetToolbarActionText(CountdownToolbarAction.GlanceSurface);
        DeleteMenuItem.Text = GetToolbarActionText(CountdownToolbarAction.Delete);
        EditMenuItem.Text = GetToolbarActionText(CountdownToolbarAction.Edit);
        AddMenuItem.Text = GetToolbarActionText(CountdownToolbarAction.Add);
        DesktopCountdownMenuItem.Text = GetToolbarActionText(CountdownToolbarAction.DesktopOverlay);
        SortCountdownsTitleText.Text = GetString("Settings/SortCountdownsTitle");
        AutoDeleteExpiredCountdownsTitleText.Text = GetString("Settings/AutoDeleteExpiredCountdownsTitle");
        AutoDeleteExpiredCountdownsDescriptionText.Text = GetString("Settings/AutoDeleteExpiredCountdownsDescription");
        ToolbarLayoutTitleText.Text = GetString("Settings/ToolbarLayoutTitle");
        ToolbarLayoutDescriptionText.Text = GetString("Settings/ToolbarLayoutSummaryDescription");
        TextSizeTitleText.Text = GetString("Settings/TextSizeTitle");
        DaysTextSizeLabelText.Text = GetString("Settings/DaysTextSizeLabel");
        TitleTextSizeLabelText.Text = GetString("Settings/TitleTextSizeLabel");
        HourDisplayModeTitleText.Text = GetString("Settings/HourDisplayModeTitle");
        HoursDisplayThresholdDaysLabelText.Text = GetString("Settings/HoursDisplayThresholdDaysLabel");
        HoursDisplayThresholdDaysSuffixText.Text = GetString("Settings/HoursDisplayThresholdDaysSuffix");
        ReduceMotionTitleText.Text = GetString("Settings/ReduceMotionTitle");
        OpenAtStartupTitleText.Text = GetString("Settings/OpenAtStartupTitle");
        ExperimentalFeaturesTitleText.Text = GetString("DesktopCountdownMenuItem/Text");
        ExperimentalFeaturesDescriptionText.Text = string.Empty;
        ExperimentalFeaturesDescriptionText.Visibility = Visibility.Collapsed;
        AutomationProperties.SetName(DesktopCountdownBackButton, GetString("Settings/BackButton"));
        ToolTipService.SetToolTip(DesktopCountdownBackButton, GetString("Settings/BackButton"));
        DesktopCountdownPageTitleText.Text = GetString("DesktopCountdown/Title");
        DesktopCountdownTitleText.Text = GetString("Settings/DesktopCountdownTitle");
        DesktopCountdownDescriptionText.Text = GetString("Settings/DesktopCountdownDescription");
        DesktopCountdownExperimentalInfoBar.Title = GetString("Settings/ExperimentalHeader");
        DesktopCountdownExperimentalInfoBar.Message = GetString("DesktopCountdown/LimitationsText");
        DesktopCountdownPlacementHeaderText.Text = GetString("DesktopCountdown/PlacementHeader");
        DesktopCountdownStyleHeaderText.Text = GetString("DesktopCountdown/StyleHeader");
        DesktopCountdownAdjustHintText.Text = GetString("Settings/DesktopCountdownAdjustHint");
        DesktopCountdownStyleTitleText.Text = GetString("Settings/DesktopCountdownStyleTitle");
        DesktopCountdownStyleDescriptionText.Text = GetString("Settings/DesktopCountdownStyleDescription");
        DesktopCountdownStyleTemplatesHeaderText.Text = GetString("DesktopCountdown/StyleTemplatesHeader");
        BuildDesktopCountdownStyleTemplateButtons();
        ResetDesktopCountdownPositionButton.Content = GetString("Settings/DesktopCountdownResetButton");
        ResetDesktopCountdownStyleButton.Content = GetString("Settings/DesktopCountdownResetStyleButton");
        AboutAppNameText.Text = GetString("AppName");
        AboutVersionText.Text = GetAppVersion();
        SourceCodeLinkButton.Content = GetString("Settings/SourceCodeLink");

        AutomationProperties.SetName(SortCountdownsToggle, SortCountdownsTitleText.Text);
        AutomationProperties.SetHelpText(SortCountdownsToggle, GetString("Settings/SortCountdownsDescription"));
        AutomationProperties.SetName(AutoDeleteExpiredCountdownsToggle, AutoDeleteExpiredCountdownsTitleText.Text);
        AutomationProperties.SetHelpText(AutoDeleteExpiredCountdownsToggle, AutoDeleteExpiredCountdownsDescriptionText.Text);
        AutomationProperties.SetName(ToolbarLayoutEntryButton, ToolbarLayoutTitleText.Text);
        AutomationProperties.SetHelpText(ToolbarLayoutEntryButton, ToolbarLayoutDescriptionText.Text);
        AutomationProperties.SetName(ToolbarLayoutListView, ToolbarLayoutPageTitleText.Text);
        AutomationProperties.SetHelpText(ToolbarLayoutListView, ToolbarLayoutPageDescriptionText.Text);
        AutomationProperties.SetName(NotifyMenuItem, NotifyMenuItem.Text);
        AutomationProperties.SetName(TileMenuItem, TileMenuItem.Text);
        AutomationProperties.SetName(DeleteMenuItem, DeleteMenuItem.Text);
        AutomationProperties.SetName(EditMenuItem, EditMenuItem.Text);
        AutomationProperties.SetName(AddMenuItem, AddMenuItem.Text);
        AutomationProperties.SetName(DesktopCountdownMenuItem, DesktopCountdownMenuItem.Text);
        AutomationProperties.SetName(ExperimentalFeaturesToggle, ExperimentalFeaturesTitleText.Text);
        AutomationProperties.SetName(DesktopCountdownBackButton, GetString("Settings/BackButton"));
        AutomationProperties.SetName(TextSizeExpander, TextSizeTitleText.Text);
        AutomationProperties.SetHelpText(TextSizeExpander, GetString("Settings/TextSizeDescription"));
        AutomationProperties.SetName(DaysTextSizeSlider, DaysTextSizeLabelText.Text);
        AutomationProperties.SetName(TitleTextSizeSlider, TitleTextSizeLabelText.Text);
        AutomationProperties.SetName(HourDisplayModeComboBox, HourDisplayModeTitleText.Text);
        AutomationProperties.SetHelpText(HourDisplayModeComboBox, GetString("Settings/HourDisplayModeDescription"));
        AutomationProperties.SetName(HoursDisplayThresholdDaysBox, GetString("Settings/HoursDisplayThresholdDaysBoxName"));
        AutomationProperties.SetHelpText(HoursDisplayThresholdDaysBox, GetString("Settings/HoursDisplayThresholdDaysHelpText"));
        AutomationProperties.SetName(ReduceMotionToggle, ReduceMotionTitleText.Text);
        AutomationProperties.SetHelpText(ReduceMotionToggle, GetString("Settings/ReduceMotionDescription"));
        AutomationProperties.SetName(OpenAtStartupToggle, OpenAtStartupTitleText.Text);
        AutomationProperties.SetHelpText(OpenAtStartupToggle, GetString("Settings/OpenAtStartupDescription"));
        AutomationProperties.SetName(DesktopCountdownToggle, DesktopCountdownTitleText.Text);
        AutomationProperties.SetHelpText(DesktopCountdownToggle, DesktopCountdownDescriptionText.Text);
        AutomationProperties.SetName(DesktopCountdownExperimentalInfoBar, DesktopCountdownExperimentalInfoBar.Title);
        AutomationProperties.SetHelpText(DesktopCountdownExperimentalInfoBar, DesktopCountdownExperimentalInfoBar.Message);
        AutomationProperties.SetName(AdjustDesktopCountdownButton, GetString("Settings/DesktopCountdownAdjustButton"));
        AutomationProperties.SetName(ResetDesktopCountdownPositionButton, GetString("Settings/DesktopCountdownResetButton"));
        AutomationProperties.SetHelpText(AdjustDesktopCountdownButton, DesktopCountdownAdjustHintText.Text);
        AutomationProperties.SetName(DesktopCountdownStyleExpander, DesktopCountdownStyleTitleText.Text);
        AutomationProperties.SetHelpText(DesktopCountdownStyleExpander, DesktopCountdownStyleDescriptionText.Text);
        AutomationProperties.SetName(DesktopCountdownCssTextBox, DesktopCountdownStyleTitleText.Text);
        AutomationProperties.SetHelpText(DesktopCountdownCssTextBox, DesktopCountdownStyleDescriptionText.Text);
        AutomationProperties.SetName(ResetDesktopCountdownStyleButton, GetString("Settings/DesktopCountdownResetStyleButton"));
        AutomationProperties.SetName(SourceCodeLinkButton, GetString("Settings/SourceCodeLink"));

        DaysTextSizeSlider.Minimum = CountdownPreferences.MinDaysTextSize;
        DaysTextSizeSlider.Maximum = CountdownPreferences.MaxDaysTextSize;
        TitleTextSizeSlider.Minimum = CountdownPreferences.MinTitleTextSize;
        TitleTextSizeSlider.Maximum = CountdownPreferences.MaxTitleTextSize;
        HoursDisplayThresholdDaysBox.Minimum = CountdownPreferences.MinHoursDisplayThresholdDays;
        HoursDisplayThresholdDaysBox.Maximum = CountdownPreferences.MaxHoursDisplayThresholdDays;
        DesktopCountdownCssTextBox.MaxLength = CountdownPreferences.MaxDesktopCountdownCssLength;
    }

    private void BuildDesktopCountdownStyleTemplateButtons()
    {
        DesktopCountdownStyleTemplatesPanel.Children.Clear();
        for (int index = 0; index < DesktopCountdownStyleTemplates.Count; index++)
        {
            DesktopCountdownStyleTemplate template = DesktopCountdownStyleTemplates[index];
            string title = GetString(template.TitleKey);
            string description = GetString(template.DescriptionKey);
            string automationName = string.Format(
                CultureInfo.CurrentCulture,
                GetString("DesktopCountdown/StyleTemplateApplyAutomationName"),
                title);

            Button button = new()
            {
                Tag = template.Css,
                Padding = new Thickness(12, 10, 12, 10),
                MinHeight = 56,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Content = CreateDesktopCountdownStyleTemplateContent(title, description)
            };
            button.Click += DesktopCountdownStyleTemplateButton_Click;
            ToolTipService.SetToolTip(button, automationName);
            AutomationProperties.SetAutomationId(button, $"DesktopCountdownStyleTemplateButton{index}");
            AutomationProperties.SetName(button, automationName);
            AutomationProperties.SetHelpText(button, description);
            DesktopCountdownStyleTemplatesPanel.Children.Add(button);
        }
    }

    private static FrameworkElement CreateDesktopCountdownStyleTemplateContent(string title, string description)
    {
        Grid layout = new()
        {
            ColumnSpacing = 12,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        StackPanel text = new()
        {
            Spacing = 2
        };
        text.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 13,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap
        });
        text.Children.Add(new TextBlock
        {
            Text = description,
            FontSize = 12,
            MaxLines = 2,
            Opacity = 0.72,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.WrapWholeWords
        });

        FontIcon icon = new()
        {
            FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
            FontSize = 16,
            Glyph = ApplyStyleTemplateGlyph,
            Opacity = 0.78,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(icon, 1);

        layout.Children.Add(text);
        layout.Children.Add(icon);
        return layout;
    }

    private void UpdateSettingsControls()
    {
        _isUpdatingSettingsControls = true;
        try
        {
            SortCountdownsToggle.IsOn = _preferences.SortCountdownsByDaysLeft;
            AutoDeleteExpiredCountdownsToggle.IsOn = _preferences.AutoDeleteExpiredCountdowns;
            OpenAtStartupToggle.IsOn = _preferences.OpenWindowAtStartup;
            DesktopOverlaySettings selectedOverlay = GetSelectedDesktopOverlay();
            DesktopCountdownToggle.IsOn = selectedOverlay.IsEnabled;
            ReduceMotionToggle.IsOn = _preferences.ReduceMotion;
            ExperimentalFeaturesToggle.IsOn = _preferences.ExperimentalFeaturesEnabled;
            UpdateExperimentalFeaturesCardState();
            HourDisplayModeComboBox.SelectedItem = _hourDisplayModeOptions.FirstOrDefault(
                option => option.Mode == _preferences.HourDisplayMode);
            HoursDisplayThresholdDaysBox.Value = _preferences.HoursDisplayThresholdDays;
            HoursDisplayThresholdDaysBox.IsEnabled = _preferences.ShowHoursWithinThreshold;
            HoursDisplayThresholdPanel.Visibility = _preferences.ShowHoursWithinThreshold
                ? Visibility.Visible
                : Visibility.Collapsed;
            DaysTextSizeSlider.Value = _preferences.DaysTextSize;
            TitleTextSizeSlider.Value = _preferences.TitleTextSize;
            if (DesktopCountdownCssTextBox.Text != selectedOverlay.Css)
            {
                DesktopCountdownCssTextBox.Text = selectedOverlay.Css;
            }
            AdjustDesktopCountdownButton.Content = GetString(_isAdjustingDesktopCountdown
                ? "Settings/DesktopCountdownDoneButton"
                : "Settings/DesktopCountdownAdjustButton");
            Visibility enabledOverlayVisibility = selectedOverlay.IsEnabled
                ? Visibility.Visible
                : Visibility.Collapsed;
            DesktopCountdownOptionsPanel.Visibility = enabledOverlayVisibility;
            DesktopCountdownStylePanel.Visibility = enabledOverlayVisibility;
            AdjustDesktopCountdownButton.IsEnabled = selectedOverlay.IsEnabled;
            ResetDesktopCountdownPositionButton.IsEnabled = selectedOverlay.IsEnabled;
            BuildToolbarLayoutListView();
            UpdateTextSizeValueText();
        }
        finally
        {
            _isUpdatingSettingsControls = false;
        }
    }

    private void UpdateExperimentalFeaturesCardState()
    {
        ExperimentalFeaturesCardBorder.Opacity = _preferences.ExperimentalFeaturesEnabled ? 1 : 0;
    }

    private void SettingsContentScroller_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        SettingsContentPanel.Width = Math.Min(640, Math.Max(360, e.NewSize.Width - 36));
    }

    private void ToolbarLayoutContentScroller_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ToolbarLayoutContentPanel.Width = Math.Min(640, Math.Max(360, e.NewSize.Width - 36));
    }

    private void BuildToolbarLayoutListView()
    {
        if (ToolbarLayoutListView is null)
        {
            return;
        }

        ToolbarLayoutListView.Items.Clear();
        IReadOnlyList<CountdownToolbarAction> actions = GetToolbarLayoutActionsForSettings();
        int primaryActionCount = GetVisiblePrimaryActionCount(actions);
        for (int index = 0; index <= actions.Count; index++)
        {
            if (index == primaryActionCount)
            {
                ToolbarLayoutListView.Items.Add(CreateToolbarLayoutDividerItem());
            }

            if (index < actions.Count)
            {
                ToolbarLayoutListView.Items.Add(CreateToolbarLayoutActionItem(actions[index]));
            }
        }
    }

    private IReadOnlyList<CountdownToolbarAction> GetToolbarLayoutActionsForSettings()
    {
        return GetEffectiveToolbarLayout().Actions
            .Where(IsToolbarActionAvailableInSettings)
            .ToList();
    }

    private int GetVisiblePrimaryActionCount(IReadOnlyList<CountdownToolbarAction> visibleActions)
    {
        HashSet<CountdownToolbarAction> visibleActionSet = new(visibleActions);
        CountdownToolbarLayout layout = GetEffectiveToolbarLayout();
        return layout.Actions
            .Take(layout.PrimaryActionCount)
            .Count(visibleActionSet.Contains);
    }

    private bool IsToolbarActionAvailableInSettings(CountdownToolbarAction action)
    {
        return action != CountdownToolbarAction.DesktopOverlay || _preferences.ExperimentalFeaturesEnabled;
    }

    private ListViewItem CreateToolbarLayoutActionItem(CountdownToolbarAction action)
    {
        ListViewItem item = new()
        {
            Tag = action,
            Padding = new Thickness(10, 6, 10, 6),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Content = CreateToolbarLayoutActionRow(action)
        };
        AutomationProperties.SetAutomationId(item, $"ToolbarLayoutAction_{action}");
        AutomationProperties.SetName(item, GetToolbarActionText(action));
        return item;
    }

    private ListViewItem CreateToolbarLayoutDividerItem()
    {
        ListViewItem item = new()
        {
            Tag = ToolbarLayoutDividerTag,
            CanDrag = false,
            Padding = new Thickness(10, 8, 10, 8),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Content = CreateToolbarLayoutDividerRow()
        };
        AutomationProperties.SetAutomationId(item, "ToolbarLayoutMoreDivider");
        AutomationProperties.SetName(item, GetString("Settings/ToolbarLayoutMoreDivider"));
        return item;
    }

    private Grid CreateToolbarLayoutActionRow(CountdownToolbarAction action)
    {
        Grid row = new()
        {
            ColumnSpacing = 12
        };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        FontIcon icon = new()
        {
            FontFamily = GetToolbarActionFontFamily(action),
            FontSize = 16,
            Glyph = GetToolbarActionGlyph(action),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(icon, 0);

        TextBlock title = new()
        {
            Text = GetToolbarActionText(action),
            TextWrapping = TextWrapping.WrapWholeWords,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(title, 1);

        FontIcon dragIcon = new()
        {
            FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
            FontSize = 14,
            Glyph = "\uE700",
            Opacity = 0.62,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(dragIcon, 2);

        row.Children.Add(icon);
        row.Children.Add(title);
        row.Children.Add(dragIcon);
        return row;
    }

    private Grid CreateToolbarLayoutDividerRow()
    {
        Grid row = new()
        {
            ColumnSpacing = 10,
            IsHitTestVisible = false
        };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        Border leftLine = CreateToolbarLayoutDividerLine();
        Grid.SetColumn(leftLine, 0);

        TextBlock text = new()
        {
            Text = GetString("Settings/ToolbarLayoutMoreDivider"),
            Opacity = 0.72,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(text, 1);

        Border rightLine = CreateToolbarLayoutDividerLine();
        Grid.SetColumn(rightLine, 2);

        row.Children.Add(leftLine);
        row.Children.Add(text);
        row.Children.Add(rightLine);
        return row;
    }

    private Border CreateToolbarLayoutDividerLine()
    {
        return new Border
        {
            Height = 1,
            Background = GetResourceBrush("ControlStrokeColorDefaultBrush", new SolidColorBrush(Colors.Gray)),
            Opacity = 0.5,
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    private string GetToolbarActionText(CountdownToolbarAction action)
    {
        return action switch
        {
            CountdownToolbarAction.StartupNotification => GetString("ToastButton/Content"),
            CountdownToolbarAction.GlanceSurface => GetString(_isWindows11OrGreater ? "WidgetButton/Tooltip" : "TileButton/Content"),
            CountdownToolbarAction.Delete => GetString("DeleteCountdownButton/Tooltip"),
            CountdownToolbarAction.Edit => GetString("EditCountdownButton/Tooltip"),
            CountdownToolbarAction.Add => GetString("AddCountdownButton/Tooltip"),
            CountdownToolbarAction.DesktopOverlay => GetString("DesktopCountdownMenuItem/Text"),
            _ => string.Empty
        };
    }

    private string GetToolbarActionGlyph(CountdownToolbarAction action)
    {
        return action switch
        {
            CountdownToolbarAction.StartupNotification => "\uE7E7",
            CountdownToolbarAction.GlanceSurface => _isWindows11OrGreater ? WidgetAddGlyph : TileGlyph,
            CountdownToolbarAction.Delete => "\uE74D",
            CountdownToolbarAction.Edit => "\uE70F",
            CountdownToolbarAction.Add => "\uE710",
            CountdownToolbarAction.DesktopOverlay => "\uE7F4",
            _ => string.Empty
        };
    }

    private FontFamily GetToolbarActionFontFamily(CountdownToolbarAction action)
    {
        return action == CountdownToolbarAction.GlanceSurface
            ? new FontFamily(_isWindows11OrGreater ? "Segoe Fluent Icons, Segoe MDL2 Assets" : "Segoe MDL2 Assets")
            : new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets");
    }

    private void DesktopCountdownContentScroller_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        DesktopCountdownContentPanel.Width = Math.Min(640, Math.Max(360, e.NewSize.Width - 36));
    }

    private bool RefreshDesktopOverlayAvailability()
    {
        _isDesktopOverlayAvailable = DesktopWorkerWindowHostResolver.TryResolve(out _);
        return _isDesktopOverlayAvailable;
    }

    private void ApplyMotionPreference()
    {
        if (AnimationsEnabled)
        {
            CountdownSelectorFrame.Transitions ??= new TransitionCollection
            {
                new RepositionThemeTransition()
            };
            CountdownSelectorItems.ChildrenTransitions ??= new TransitionCollection
            {
                new RepositionThemeTransition()
            };
            return;
        }

        _mainContentOffsetStoryboard?.Stop();
        _mainContentOffsetStoryboard = null;
        _countdownSurfaceStoryboard?.Stop();
        _countdownSurfaceStoryboard = null;
        _countdownSelectorVisibilityStoryboard?.Stop();
        _countdownSelectorVisibilityStoryboard = null;
        CountdownSelectorFrame.Transitions = null;
        CountdownSelectorItems.ChildrenTransitions = null;
        if (!_isEditorVisible)
        {
            SetCountdownSurfaceVisible();
        }
    }

    private static string GetAppVersion()
    {
        try
        {
            PackageVersion version = Package.Current.Id.Version;
            return $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
        }
        catch
        {
            return string.Empty;
        }
    }

    private void ApplyDisplaySizePreference()
    {
        ApplyCountdownTextStyle(_state.HasCountdowns || _isEditorVisible);
        MainContentPanel.MaxWidth = Math.Clamp(_preferences.DaysTextSize * 5.4, 360, 560);
        MainContentPanel.Spacing = Math.Clamp(_preferences.TitleTextSize + 12, 24, 34);
        UpdateMainContentPanelPadding();
        UpdateSettingsPreview();
        UpdateTextSizeValueText();
        UpdateDesktopCountdown();
    }

    private void UpdateMainContentPanelPadding(bool? selectorVisible = null)
    {
        double viewportHeight = MainContentScroller.ActualHeight;
        if (viewportHeight <= 0)
        {
            return;
        }

        double displayHeight = GetCountdownSurfaceHeight();
        double editorHeight = _isEditorVisible
            ? GetVisibleElementHeight(InlineEditorPanel, 166)
            : 0;
        double toolbarHeight = GetVisibleElementHeight(ToggleButtons, 40);
        bool isSelectorVisible = selectorVisible ??
            CountdownSelectorFrame.Visibility == Visibility.Visible ||
            _state.Countdowns.Count > 1;
        MainContentPadding padding = CountdownMainContentLayout.CalculatePadding(
            viewportHeight,
            displayHeight,
            editorHeight,
            toolbarHeight,
            MainContentPanel.Spacing,
            isSelectorVisible,
            _isEditorVisible);

        MainContentPanel.Padding = new Thickness(32, padding.Top, 32, padding.Bottom);
    }

    private double GetCountdownSurfaceHeight()
    {
        if (CountdownDisplayPanel.Visibility == Visibility.Visible)
        {
            return GetVisibleElementHeight(CountdownDisplayPanel, 140);
        }

        return 0;
    }

    private static double GetVisibleElementHeight(FrameworkElement element, double fallback)
    {
        return element.Visibility == Visibility.Visible && element.ActualHeight > 0
            ? element.ActualHeight
            : fallback;
    }

    private void UpdateEditorFromState()
    {
        UpdateCountdownPreview();
        UpdateCountdownSelector();
        if (_state.SelectedCountdown is CountdownItem selectedCountdown)
        {
            UpdateEditorFromCountdown(selectedCountdown);
        }
        else if (_isEditorVisible)
        {
            ExitEditorMode(restorePreview: false);
        }
    }

    private void UpdateEditorFromCountdown(CountdownItem countdown)
    {
        EditorTitleText.Text = GetString("Editor/Title/Edit");
        AutomationProperties.SetName(InlineEditorPanel, EditorTitleText.Text);
        TextBoxTitle.PlaceholderText = GetString("Title/PlaceholderText");
        TextBoxTitle.MaxLength = CountdownItem.MaxTitleLength;
        TextBoxTitle.Text = countdown.Title;
        SelectTargetTimeZone(countdown.TargetTimeZoneId);
        UpdateTargetDatePickerMinDate(countdown.TargetTimeZoneId, countdown.TargetDate);
        DateTime targetDate = CountdownTimeZone.GetDateInTimeZone(countdown.TargetDate, countdown.TargetTimeZoneId);
        DatePickerTargetDate.Date = new DateTimeOffset(targetDate, TimeSpan.Zero);
        TimePickerTargetTime.Time = CountdownTimeZone.GetTimeOfDayInTimeZone(countdown.TargetDate, countdown.TargetTimeZoneId);
        bool showTime = UsesCustomDeadlineTime(countdown);
        bool showTimeZone = UsesCustomDeadlineTimeZone(countdown);
        EditorTimeOptionsButton.IsChecked = showTime;
        EditorTimeZoneOptionsButton.IsChecked = showTimeZone;
        UpdateEditorOptionToggleVisuals();
        SetEditorOptionVisible(
            TimePickerTargetTime,
            showTime,
            animate: false);
        SetEditorOptionVisible(
            ComboBoxTargetTimeZone,
            showTimeZone,
            animate: false);
        UpdateEditorCommandButtons();
    }

    private void UpdateEditorOptionToggleVisuals()
    {
        SetEditorOptionToggleVisual(EditorTimeOptionsButton);
        SetEditorOptionToggleVisual(EditorTimeZoneOptionsButton);
    }

    private void SetEditorOptionToggleVisual(ToggleButton button)
    {
        bool isActive = button.IsChecked == true;
        button.Background = isActive
            ? new SolidColorBrush(Colors.DeepSkyBlue) { Opacity = 0.12 }
            : new SolidColorBrush(Colors.Transparent);
        button.BorderBrush = isActive
            ? GetResourceBrush("AccentFillColorDefaultBrush", new SolidColorBrush(Colors.DeepSkyBlue))
            : new SolidColorBrush(Colors.Transparent);
        button.BorderThickness = isActive ? new Thickness(1) : new Thickness(0);
        button.Foreground = GetResourceBrush("TextFillColorPrimaryBrush", new SolidColorBrush(Colors.White));
    }

    private static bool UsesAdvancedDeadlineOptions(CountdownItem countdown)
    {
        return UsesCustomDeadlineTimeZone(countdown) || UsesCustomDeadlineTime(countdown);
    }

    private static bool UsesCustomDeadlineTimeZone(CountdownItem countdown)
    {
        return !string.Equals(
            CountdownTimeZone.NormalizeTimeZoneId(countdown.TargetTimeZoneId),
            CountdownTimeZone.DefaultTimeZoneId,
            StringComparison.Ordinal);
    }

    private static bool UsesCustomDeadlineTime(CountdownItem countdown)
    {
        return CountdownTimeZone.GetTimeOfDayInTimeZone(countdown.TargetDate, countdown.TargetTimeZoneId) !=
            CountdownTimeZone.DefaultDeadlineTimeOfDay;
    }

    private void UpdateTargetDatePickerMinDate(string timeZoneId, DateTimeOffset targetDate)
    {
        DateTime today = CountdownTimeZone.GetDateInTimeZone(DateTimeOffset.Now, timeZoneId);
        DateTime selectedDate = CountdownTimeZone.GetDateInTimeZone(targetDate, timeZoneId);
        DatePickerTargetDate.MinDate = new DateTimeOffset(selectedDate < today ? selectedDate : today, TimeSpan.Zero);
    }

    private void SelectTargetTimeZone(string timeZoneId)
    {
        string normalizedTimeZoneId = CountdownTimeZone.NormalizeTimeZoneId(timeZoneId);
        CountdownTimeZoneOption? option = _timeZoneOptions.FirstOrDefault(
            candidate => string.Equals(candidate.Id, normalizedTimeZoneId, StringComparison.Ordinal));
        if (option is null)
        {
            option = new CountdownTimeZoneOption(
                normalizedTimeZoneId,
                CountdownTimeZone.FindTimeZoneOrLocal(normalizedTimeZoneId).DisplayName);
            List<CountdownTimeZoneOption> options = _timeZoneOptions.ToList();
            options.Add(option);
            _timeZoneOptions = options;
            ComboBoxTargetTimeZone.ItemsSource = _timeZoneOptions;
        }

        ComboBoxTargetTimeZone.SelectedItem = option;
    }

    private void UpdateCountdownPreview()
    {
        if (_state.SelectedCountdown is not CountdownItem selectedCountdown)
        {
            _previewTargetDate = CreateDefaultTargetDate();
            TextBlockDays.Text = GetString("EmptyState/Title");
            TextBlockTitle.Text = string.Empty;
            ApplyCountdownTextStyle(hasCountdownText: false);
            AutomationProperties.SetName(CountdownDisplayPanel, TextBlockDays.Text);
            SetCountdownSurfaceVisible();
            UpdateSettingsPreview();
            UpdateDesktopCountdown();
            return;
        }

        _previewTargetDate = selectedCountdown.TargetDate;
        TextBlockTitle.Text = selectedCountdown.Title;
        TextBlockDays.Text = FormatCountdown(_previewTargetDate);
        ApplyCountdownTextStyle(hasCountdownText: true);
        AutomationProperties.SetName(CountdownDisplayPanel, selectedCountdown.Title);
        SetCountdownSurfaceVisible();
        UpdateSettingsPreview();
        UpdateDesktopCountdown();
    }

    private void ApplyCountdownTextStyle(bool hasCountdownText)
    {
        TextBlockDays.FontSize = hasCountdownText ? _preferences.DaysTextSize : 32;
        TextBlockDays.Opacity = hasCountdownText ? 1 : 0.72;
        TextBlockTitle.FontSize = _preferences.TitleTextSize;
        TextBlockTitle.Visibility = hasCountdownText ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SetCountdownSurfaceVisible()
    {
        _countdownSurfaceStoryboard?.Stop();
        _countdownSurfaceStoryboard = null;

        if (!_isMotionReady || !AnimationsEnabled || _isEditorVisible)
        {
            CountdownDisplayPanel.Visibility = Visibility.Visible;
            CountdownDisplayPanel.Opacity = 1;
            UpdateMainContentPanelPadding();
            return;
        }

        if (CountdownDisplayPanel.Visibility == Visibility.Visible && CountdownDisplayPanel.Opacity >= 1)
        {
            UpdateMainContentPanelPadding();
            return;
        }

        if (CountdownDisplayPanel.Visibility != Visibility.Visible)
        {
            CountdownDisplayPanel.Opacity = 0;
            CountdownDisplayPanel.Visibility = Visibility.Visible;
        }

        Storyboard storyboard = CreateOpacityStoryboard(CountdownDisplayPanel, 1);
        _countdownSurfaceStoryboard = storyboard;
        storyboard.Completed += (_, _) =>
        {
            if (ReferenceEquals(_countdownSurfaceStoryboard, storyboard))
            {
                _countdownSurfaceStoryboard = null;
                CountdownDisplayPanel.Visibility = Visibility.Visible;
                CountdownDisplayPanel.Opacity = 1;
                UpdateMainContentPanelPadding();
            }
        };
        storyboard.Begin();
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
            ReconcileDesktopOverlaySettingsWithState(saveIfChanged: true);
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

    private void UpdateSettingsPreview()
    {
        if (SettingsPreviewDaysText is null || SettingsPreviewTitleText is null)
        {
            return;
        }

        bool hasCountdown = _state.HasCountdowns;
        SettingsPreviewDaysText.Text = hasCountdown
            ? FormatCountdown(_previewTargetDate)
            : GetString("EmptyState/Title");
        SettingsPreviewDaysText.FontSize = _preferences.DaysTextSize;
        SettingsPreviewTitleText.Text = !hasCountdown
            ? string.Empty
            : string.IsNullOrWhiteSpace(_state.Title)
            ? GetString("Title/PlaceholderText")
            : _state.Title;
        SettingsPreviewTitleText.FontSize = _preferences.TitleTextSize;
    }

    private void UpdateTextSizeValueText()
    {
        if (DaysTextSizeValueText is null || TitleTextSizeValueText is null)
        {
            return;
        }

        DaysTextSizeValueText.Text = string.Format(CultureInfo.CurrentCulture, "{0:0}", _preferences.DaysTextSize);
        TitleTextSizeValueText.Text = string.Format(CultureInfo.CurrentCulture, "{0:0}", _preferences.TitleTextSize);
    }

    private void UpdateButtonStatus()
    {
        if (_isEditorVisible)
        {
            ApplyEditorToolbarState();
            NotifyButton.Foreground = GetToggleBrush(_state.ToastEnabled);
            UpdateGlanceSurfaceStatus();
            return;
        }

        bool canCommitDraft = CreateDraftFromState().CanCommit(DateTimeOffset.Now);
        ApplyToolbarButtonStates([
            CreateToolbarButtonState(CountdownToolbarAction.StartupNotification, canCommitDraft),
            CreateToolbarButtonState(CountdownToolbarAction.GlanceSurface, canCommitDraft),
            CreateToolbarButtonState(CountdownToolbarAction.Delete, canCommitDraft),
            new ToolbarButtonState(CancelEditorButton, IsVisible: false, IsEnabled: false, IsActive: false),
            new ToolbarButtonState(SaveEditorButton, IsVisible: false, IsEnabled: false, IsActive: false),
            CreateToolbarButtonState(CountdownToolbarAction.Edit, canCommitDraft),
            CreateToolbarButtonState(CountdownToolbarAction.Add, canCommitDraft),
            CreateToolbarButtonState(CountdownToolbarAction.DesktopOverlay, canCommitDraft),
            new ToolbarButtonState(MoreButton, IsVisible: true, IsEnabled: true, IsActive: false)
        ], GetNormalToolbarButtonOrder());

        NotifyButton.Foreground = GetToggleBrush(_state.ToastEnabled);
        UpdateGlanceSurfaceStatus();
        UpdateMoreMenuItems(canCommitDraft);
    }

    private void ApplyEditorToolbarState()
    {
        bool canSaveDraft = CreateDraftFromControls().CanCommit(DateTimeOffset.Now);
        ApplyToolbarButtonStates([
            new ToolbarButtonState(NotifyButton, IsVisible: false, IsEnabled: false, IsActive: false),
            new ToolbarButtonState(TileButton, IsVisible: false, IsEnabled: false, IsActive: false),
            new ToolbarButtonState(DeleteButton, IsVisible: false, IsEnabled: false, IsActive: false),
            new ToolbarButtonState(CancelEditorButton, IsVisible: true, IsEnabled: true, IsActive: false),
            new ToolbarButtonState(SaveEditorButton, IsVisible: true, IsEnabled: canSaveDraft, IsActive: canSaveDraft),
            new ToolbarButtonState(EditButton, IsVisible: false, IsEnabled: false, IsActive: false),
            new ToolbarButtonState(AddButton, IsVisible: false, IsEnabled: false, IsActive: false),
            new ToolbarButtonState(DesktopOverlayButton, IsVisible: false, IsEnabled: false, IsActive: false),
            new ToolbarButtonState(MoreButton, IsVisible: false, IsEnabled: false, IsActive: false)
        ], GetEditorToolbarButtonOrder());
        UpdateMoreMenuItems(canCommitDraft: false);
    }

    private ToolbarButtonState CreateToolbarButtonState(CountdownToolbarAction action, bool canCommitDraft)
    {
        bool isVisible = IsToolbarActionPrimary(action) && IsToolbarActionVisible(action);
        return new ToolbarButtonState(
            GetToolbarActionButton(action),
            isVisible,
            IsToolbarActionEnabled(action, canCommitDraft),
            IsActive: false);
    }

    private bool IsToolbarActionVisible(CountdownToolbarAction action)
    {
        return action switch
        {
            CountdownToolbarAction.StartupNotification => _state.HasCountdowns,
            CountdownToolbarAction.GlanceSurface => _state.HasCountdowns,
            CountdownToolbarAction.Delete => _state.CanRemoveCountdown,
            CountdownToolbarAction.Edit => _state.HasCountdowns,
            CountdownToolbarAction.DesktopOverlay => _state.HasCountdowns && _preferences.ExperimentalFeaturesEnabled && _isDesktopOverlayAvailable,
            _ => true
        };
    }

    private bool IsToolbarActionPrimary(CountdownToolbarAction action)
    {
        return GetEffectiveToolbarLayout().IsPrimary(action);
    }

    private CountdownToolbarLayout GetEffectiveToolbarLayout()
    {
        return CountdownToolbarLayoutPolicy.ResolveEffectiveLayout(
            _preferences.ToolbarLayout,
            _preferences.ExperimentalFeaturesEnabled);
    }

    private bool IsToolbarActionEnabled(CountdownToolbarAction action, bool canCommitDraft)
    {
        return action switch
        {
            CountdownToolbarAction.StartupNotification => _state.ToastEnabled || canCommitDraft,
            CountdownToolbarAction.GlanceSurface => _state.HasCountdowns && (_isWindows11OrGreater || _state.TileEnabled || canCommitDraft),
            CountdownToolbarAction.Delete => _state.CanRemoveCountdown,
            CountdownToolbarAction.Edit => _state.HasCountdowns,
            CountdownToolbarAction.Add => true,
            CountdownToolbarAction.DesktopOverlay => _state.HasCountdowns && _preferences.ExperimentalFeaturesEnabled && _isDesktopOverlayAvailable,
            _ => false
        };
    }

    private Button GetToolbarActionButton(CountdownToolbarAction action)
    {
        return action switch
        {
            CountdownToolbarAction.StartupNotification => NotifyButton,
            CountdownToolbarAction.GlanceSurface => TileButton,
            CountdownToolbarAction.Delete => DeleteButton,
            CountdownToolbarAction.Edit => EditButton,
            CountdownToolbarAction.Add => AddButton,
            CountdownToolbarAction.DesktopOverlay => DesktopOverlayButton,
            _ => MoreButton
        };
    }

    private void ApplyToolbarButtonStates(IReadOnlyList<ToolbarButtonState> states, IReadOnlyList<Button>? buttonOrder = null)
    {
        if (buttonOrder is not null)
        {
            ReorderToolbarButtons(buttonOrder);
        }

        foreach (ToolbarButtonState state in states)
        {
            ApplyToolbarButtonState(state);
        }
    }

    private void ApplyToolbarButtonState(ToolbarButtonState state)
    {
        state.Button.Visibility = state.IsVisible ? Visibility.Visible : Visibility.Collapsed;
        state.Button.IsHitTestVisible = state.IsVisible && state.IsEnabled;
        state.Button.IsTabStop = state.IsVisible && state.IsEnabled;
        SetToolbarButtonState(state.Button, state.IsEnabled, state.IsActive);
    }

    private IReadOnlyList<Button> GetNormalToolbarButtonOrder()
    {
        List<Button> orderedButtons = GetEffectiveToolbarLayout().Actions
            .Select(GetToolbarActionButton)
            .Distinct()
            .ToList();
        AddMissingToolbarButtons(orderedButtons);
        return orderedButtons;
    }

    private IReadOnlyList<Button> GetEditorToolbarButtonOrder()
    {
        List<Button> orderedButtons =
        [
            CancelEditorButton,
            SaveEditorButton
        ];
        AddMissingToolbarButtons(orderedButtons);
        return orderedButtons;
    }

    private void AddMissingToolbarButtons(List<Button> orderedButtons)
    {
        foreach (Button button in GetToolbarButtons())
        {
            if (!orderedButtons.Contains(button))
            {
                orderedButtons.Add(button);
            }
        }
    }

    private void ReorderToolbarButtons(IReadOnlyList<Button> orderedButtons)
    {
        if (ToggleButtons.Children.Count == orderedButtons.Count &&
            orderedButtons.Select((button, index) => ReferenceEquals(button, ToggleButtons.Children[index])).All(isSame => isSame))
        {
            return;
        }

        ToggleButtons.Children.Clear();
        foreach (Button button in orderedButtons)
        {
            ToggleButtons.Children.Add(button);
        }
    }

    private void SetToolbarButtonState(Button button, bool isEnabled, bool isActive)
    {
        button.IsEnabled = isEnabled;
        button.Opacity = isEnabled ? 1 : 0.38;
        button.Background = isActive
            ? GetResourceBrush("AccentFillColorTertiaryBrush", new SolidColorBrush(Colors.DeepSkyBlue) { Opacity = 0.24 })
            : new SolidColorBrush(Colors.Transparent);
        button.BorderBrush = isActive
            ? GetResourceBrush("AccentFillColorDefaultBrush", new SolidColorBrush(Colors.DeepSkyBlue))
            : new SolidColorBrush(Colors.Transparent);
        button.BorderThickness = isActive ? new Thickness(1) : new Thickness(0);
    }

    private void UpdateMoreMenuItems(bool canCommitDraft)
    {
        bool anyToolbarActionInMenu = false;
        foreach (CountdownToolbarAction action in GetEffectiveToolbarLayout().Actions)
        {
            MenuFlyoutItem menuItem = GetToolbarActionMenuItem(action);
            bool isVisible = !IsToolbarActionPrimary(action) && IsToolbarActionVisible(action);
            menuItem.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
            menuItem.IsEnabled = IsToolbarActionEnabled(action, canCommitDraft);
            menuItem.Text = GetToolbarActionText(action);
            AutomationProperties.SetName(menuItem, menuItem.Text);
            anyToolbarActionInMenu |= isVisible;
        }

        ToolbarActionsSeparator.Visibility = anyToolbarActionInMenu
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private MenuFlyoutItem GetToolbarActionMenuItem(CountdownToolbarAction action)
    {
        return action switch
        {
            CountdownToolbarAction.StartupNotification => NotifyMenuItem,
            CountdownToolbarAction.GlanceSurface => TileMenuItem,
            CountdownToolbarAction.Delete => DeleteMenuItem,
            CountdownToolbarAction.Edit => EditMenuItem,
            CountdownToolbarAction.Add => AddMenuItem,
            CountdownToolbarAction.DesktopOverlay => DesktopCountdownMenuItem,
            _ => SettingsMenuItem
        };
    }

    private void UpdateCountdownSelector()
    {
        if (_isSettingsVisible || _isDesktopCountdownPageVisible || _isToolbarLayoutPageVisible)
        {
            HideCountdownSelectorImmediately(clearItems: true);
            return;
        }

        bool wasUpdatingControls = _isUpdatingControls;
        _isUpdatingControls = true;
        try
        {
            if (_state.Countdowns.Count <= 1)
            {
                SetCountdownSelectorVisible(false, clearAfterHide: true);
                return;
            }

            UpdateCountdownSelectorContentWidth();
            UpdateCountdownSelectorItems();
            BringSelectedCountdownIntoView();
            SetCountdownSelectorVisible(true);
        }
        finally
        {
            _isUpdatingControls = wasUpdatingControls;
        }
    }

    private void SetCountdownSelectorVisible(bool isVisible, bool clearAfterHide = false)
    {
        _countdownSelectorVisibilityStoryboard?.Stop();
        if (!_isEditorVisible)
        {
            AnimateMainContentNavigationOffset(isVisible);
        }
        else
        {
            ResetMainContentNavigationOffset();
            UpdateMainContentPanelPadding(isVisible);
        }

        if (!_isMotionReady || !AnimationsEnabled)
        {
            CountdownSelectorFrame.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
            CountdownSelectorFrame.Opacity = isVisible ? 1 : 0;
            if (!isVisible && clearAfterHide)
            {
                CountdownSelectorItems.Children.Clear();
            }

            return;
        }

        if (isVisible)
        {
            if (CountdownSelectorFrame.Visibility == Visibility.Visible && CountdownSelectorFrame.Opacity >= 1)
            {
                return;
            }

            CountdownSelectorFrame.Visibility = Visibility.Visible;
            CountdownSelectorFrame.Opacity = 0;
            Storyboard storyboard = CreateOpacityStoryboard(CountdownSelectorFrame, 1);
            _countdownSelectorVisibilityStoryboard = storyboard;
            storyboard.Completed += (_, _) =>
            {
                if (ReferenceEquals(_countdownSelectorVisibilityStoryboard, storyboard))
                {
                    _countdownSelectorVisibilityStoryboard = null;
                    CountdownSelectorFrame.Opacity = 1;
                }
            };
            storyboard.Begin();
            return;
        }

        if (CountdownSelectorFrame.Visibility != Visibility.Visible)
        {
            if (clearAfterHide)
            {
                CountdownSelectorItems.Children.Clear();
            }

            return;
        }

        Storyboard hideStoryboard = CreateOpacityStoryboard(CountdownSelectorFrame, 0);
        _countdownSelectorVisibilityStoryboard = hideStoryboard;
        hideStoryboard.Completed += (_, _) =>
        {
            if (ReferenceEquals(_countdownSelectorVisibilityStoryboard, hideStoryboard))
            {
                _countdownSelectorVisibilityStoryboard = null;
                CountdownSelectorFrame.Visibility = Visibility.Collapsed;
                CountdownSelectorFrame.Opacity = 0;
                if (clearAfterHide)
                {
                    CountdownSelectorItems.Children.Clear();
                }
            }
        };
        hideStoryboard.Begin();
    }

    private void HideCountdownSelectorImmediately(bool clearItems)
    {
        _countdownSelectorVisibilityStoryboard?.Stop();
        _countdownSelectorVisibilityStoryboard = null;
        CountdownSelectorFrame.Visibility = Visibility.Collapsed;
        CountdownSelectorFrame.Opacity = 0;
        ResetMainContentNavigationOffset();
        UpdateMainContentPanelPadding(selectorVisible: false);
        if (clearItems)
        {
            CountdownSelectorItems.Children.Clear();
        }
    }

    private void ResetMainContentNavigationOffset()
    {
        _mainContentOffsetStoryboard?.Stop();
        _mainContentOffsetStoryboard = null;
        EnsureMainContentTranslateTransform().Y = 0;
    }

    private void AnimateMainContentNavigationOffset(bool isSelectorVisible)
    {
        double targetOffset = isSelectorVisible ? MainContentNavigationOffset : 0;
        TranslateTransform transform = EnsureMainContentTranslateTransform();

        _mainContentOffsetStoryboard?.Stop();
        _mainContentOffsetStoryboard = null;

        if (!_isMotionReady || !AnimationsEnabled)
        {
            transform.Y = targetOffset;
            return;
        }

        if (Math.Abs(transform.Y - targetOffset) < 0.5)
        {
            transform.Y = targetOffset;
            return;
        }

        DoubleAnimation animation = new()
        {
            From = transform.Y,
            To = targetOffset,
            Duration = VisibilityMotionDuration,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            EnableDependentAnimation = true
        };
        Storyboard.SetTarget(animation, transform);
        Storyboard.SetTargetProperty(animation, nameof(TranslateTransform.Y));

        Storyboard storyboard = new();
        storyboard.Children.Add(animation);
        _mainContentOffsetStoryboard = storyboard;
        storyboard.Completed += (_, _) =>
        {
            if (ReferenceEquals(_mainContentOffsetStoryboard, storyboard))
            {
                _mainContentOffsetStoryboard = null;
                transform.Y = targetOffset;
            }
        };
        storyboard.Begin();
    }

    private TranslateTransform EnsureMainContentTranslateTransform()
    {
        if (MainContentScroller.RenderTransform is TranslateTransform transform)
        {
            return transform;
        }

        TranslateTransform newTransform = new();
        MainContentScroller.RenderTransform = newTransform;
        return newTransform;
    }

    private IReadOnlyList<Button> GetToolbarButtons()
    {
        return new[] { NotifyButton, TileButton, DeleteButton, CancelEditorButton, SaveEditorButton, EditButton, AddButton, DesktopOverlayButton, MoreButton };
    }

    private static DoubleAnimation CreateOpacityAnimation(UIElement element, double to)
    {
        DoubleAnimation animation = new()
        {
            To = to,
            Duration = VisibilityMotionDuration,
            EnableDependentAnimation = true
        };
        Storyboard.SetTarget(animation, element);
        Storyboard.SetTargetProperty(animation, nameof(UIElement.Opacity));
        return animation;
    }

    private static Storyboard CreateOpacityStoryboard(UIElement element, double to)
    {
        Storyboard storyboard = new();
        storyboard.Children.Add(CreateOpacityAnimation(element, to));
        return storyboard;
    }

    private static DoubleAnimation CreateTranslateYAnimation(TranslateTransform transform, double to)
    {
        DoubleAnimation animation = new()
        {
            To = to,
            Duration = VisibilityMotionDuration,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            EnableDependentAnimation = true
        };
        Storyboard.SetTarget(animation, transform);
        Storyboard.SetTargetProperty(animation, nameof(TranslateTransform.Y));
        return animation;
    }

    private Task AnimateCountdownSelectorItemRemovalAsync(string countdownId)
    {
        if (!_isMotionReady || !AnimationsEnabled)
        {
            return Task.CompletedTask;
        }

        FrameworkElement? item = FindCountdownSelectorItem(countdownId);
        if (item is null || item.Visibility != Visibility.Visible)
        {
            return Task.CompletedTask;
        }

        TaskCompletionSource completion = new();
        Storyboard storyboard = CreateOpacityStoryboard(item, 0);
        storyboard.Completed += (_, _) => completion.TrySetResult();
        storyboard.Begin();
        return completion.Task;
    }

    private FrameworkElement CreateCountdownSelectorItem(CountdownItem countdown, int index, bool isSelected)
    {
        string text = GetCountdownSelectorText(countdown, index);
        TextBlock label = new TextBlock
        {
            Text = text,
            FontSize = 14,
            Foreground = GetResourceBrush("TextFillColorPrimaryBrush", new SolidColorBrush(Colors.White)),
            MaxWidth = 180,
            TextAlignment = TextAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap
        };

        Border indicator = new Border
        {
            Width = 16,
            Height = 3,
            Margin = new Thickness(0, 4, 0, 0),
            Background = isSelected
                ? GetResourceBrush("AccentFillColorDefaultBrush", new SolidColorBrush(Colors.DeepSkyBlue))
                : new SolidColorBrush(Colors.Transparent),
            CornerRadius = new CornerRadius(2),
            HorizontalAlignment = HorizontalAlignment.Center
        };

        StackPanel content = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center
        };
        content.Children.Add(label);
        content.Children.Add(indicator);

        Border item = new Border
        {
            Tag = countdown.Id,
            CornerRadius = new CornerRadius(4),
            MinHeight = 32,
            Padding = new Thickness(8, 0, 8, 0),
            Background = new SolidColorBrush(Colors.Transparent),
            Child = content
        };
        item.PointerEntered += CountdownSelectorItem_PointerEntered;
        item.PointerExited += CountdownSelectorItem_PointerExited;
        item.PointerPressed += CountdownSelectorItem_PointerPressed;
        item.PointerReleased += CountdownSelectorItem_PointerReleased;
        item.PointerCanceled += CountdownSelectorItem_PointerCanceled;
        item.Tapped += CountdownSelectorItem_Tapped;
        AutomationProperties.SetName(item, text);

        return item;
    }

    private void UpdateCountdownSelectorItems()
    {
        Dictionary<string, Border> existingItems = new(StringComparer.Ordinal);
        foreach (UIElement child in CountdownSelectorItems.Children)
        {
            if (child is Border item && item.Tag is string countdownId)
            {
                existingItems[countdownId] = item;
            }
        }

        List<UIElement> orderedItems = new();
        for (int i = 0; i < _state.Countdowns.Count; i++)
        {
            CountdownItem countdown = _state.Countdowns[i];
            bool isSelected = string.Equals(countdown.Id, _state.SelectedCountdownId, StringComparison.Ordinal);
            Border item = existingItems.TryGetValue(countdown.Id, out Border? existingItem)
                ? existingItem
                : (Border)CreateCountdownSelectorItem(countdown, i, isSelected);

            UpdateCountdownSelectorItem(item, countdown, i, isSelected);
            orderedItems.Add(item);
        }

        HashSet<UIElement> orderedSet = new(orderedItems);
        for (int i = CountdownSelectorItems.Children.Count - 1; i >= 0; i--)
        {
            if (!orderedSet.Contains(CountdownSelectorItems.Children[i]))
            {
                CountdownSelectorItems.Children.RemoveAt(i);
            }
        }

        for (int i = 0; i < orderedItems.Count; i++)
        {
            UIElement item = orderedItems[i];
            int currentIndex = CountdownSelectorItems.Children.IndexOf(item);
            if (currentIndex == i)
            {
                continue;
            }

            if (currentIndex >= 0)
            {
                CountdownSelectorItems.Children.RemoveAt(currentIndex);
            }

            CountdownSelectorItems.Children.Insert(i, item);
        }
    }

    private void UpdateCountdownSelectorItem(Border item, CountdownItem countdown, int index, bool isSelected)
    {
        string text = GetCountdownSelectorText(countdown, index);
        item.Tag = countdown.Id;
        AutomationProperties.SetName(item, text);

        if (item.Child is not StackPanel content || content.Children.Count < 2)
        {
            return;
        }

        if (content.Children[0] is TextBlock label)
        {
            label.Text = text;
        }

        if (content.Children[1] is Border indicator)
        {
            indicator.Background = isSelected
                ? GetResourceBrush("AccentFillColorDefaultBrush", new SolidColorBrush(Colors.DeepSkyBlue))
                : new SolidColorBrush(Colors.Transparent);
        }

        SetCountdownSelectorItemVisual(item, isPointerOver: false);
    }

    private void SetCountdownSelectorItemVisual(Border item, bool isPointerOver, bool isPressed = false)
    {
        if (item.Child is not StackPanel content || content.Children.Count == 0 || content.Children[0] is not TextBlock label)
        {
            return;
        }

        if (isPressed)
        {
            item.Background = new SolidColorBrush(Colors.Transparent);
            label.Foreground = GetResourceBrush("TextFillColorTertiaryBrush", new SolidColorBrush(Colors.Gray));
            return;
        }

        if (isPointerOver)
        {
            item.Background = new SolidColorBrush(Colors.Transparent);
            label.Foreground = GetResourceBrush("TextFillColorSecondaryBrush", new SolidColorBrush(Colors.LightGray));
            return;
        }

        item.Background = new SolidColorBrush(Colors.Transparent);
        label.Foreground = GetResourceBrush("TextFillColorPrimaryBrush", new SolidColorBrush(Colors.White));
    }

    private void ResetCountdownSelectorItemVisuals()
    {
        foreach (UIElement selectorItem in CountdownSelectorItems.Children)
        {
            if (selectorItem is Border item)
            {
                SetCountdownSelectorItemVisual(item, isPointerOver: false);
            }
        }
    }

    private void UpdateCountdownSelectorContentWidth()
    {
        CountdownSelectorContentHost.MinWidth = Math.Max(0, CountdownSelectorRail.ViewportWidth);
    }

    private void BringSelectedCountdownIntoView()
    {
        if (!_state.HasCountdowns)
        {
            return;
        }

        string countdownId = _state.SelectedCountdownId;
        _ = BringCountdownSelectorItemIntoViewAsync(countdownId);
    }

    private async Task BringCountdownSelectorItemIntoViewAsync(string countdownId)
    {
        for (int attempt = 0; attempt < 4; attempt++)
        {
            if (attempt == 0)
            {
                await Task.Yield();
            }
            else
            {
                await Task.Delay(50);
            }

            FrameworkElement? item = FindCountdownSelectorItem(countdownId);
            if (item is null)
            {
                return;
            }

            ScrollCountdownSelectorItemIntoView(item);
            if (IsCountdownSelectorItemFullyVisible(item))
            {
                return;
            }
        }
    }

    private FrameworkElement? FindCountdownSelectorItem(string countdownId)
    {
        foreach (UIElement selectorItem in CountdownSelectorItems.Children)
        {
            if (selectorItem is FrameworkElement item &&
                item.Tag is string itemCountdownId &&
                string.Equals(itemCountdownId, countdownId, StringComparison.Ordinal))
            {
                return item;
            }
        }

        return null;
    }

    private bool IsCountdownSelectorItemFullyVisible(FrameworkElement item)
    {
        if (CountdownSelectorRail.ViewportWidth <= 0)
        {
            return false;
        }

        Point itemPosition = item.TransformToVisual(CountdownSelectorContentHost).TransformPoint(new Point(0, 0));
        double itemLeft = itemPosition.X;
        double itemRight = itemLeft + item.ActualWidth;
        double viewportLeft = CountdownSelectorRail.HorizontalOffset;
        double viewportRight = viewportLeft + CountdownSelectorRail.ViewportWidth;

        return itemLeft >= viewportLeft && itemRight <= viewportRight;
    }

    private void ScrollCountdownSelectorItemIntoView(FrameworkElement item)
    {
        if (_state.Countdowns.Count <= 1 || CountdownSelectorRail.ViewportWidth <= 0)
        {
            return;
        }

        CountdownSelectorRail.UpdateLayout();
        CountdownSelectorContentHost.UpdateLayout();

        Point itemPosition = item.TransformToVisual(CountdownSelectorContentHost).TransformPoint(new Point(0, 0));
        double itemLeft = itemPosition.X;
        double itemRight = itemLeft + item.ActualWidth;
        double viewportLeft = CountdownSelectorRail.HorizontalOffset;
        double viewportRight = viewportLeft + CountdownSelectorRail.ViewportWidth;
        const double padding = 24;

        double targetOffset = viewportLeft;
        if (itemLeft < viewportLeft + padding)
        {
            targetOffset = itemLeft - padding;
        }
        else if (itemRight > viewportRight - padding)
        {
            targetOffset = itemRight - CountdownSelectorRail.ViewportWidth + padding;
        }

        targetOffset = Math.Clamp(targetOffset, 0, CountdownSelectorRail.ScrollableWidth);
        CountdownSelectorRail.ChangeView(targetOffset, null, null);
    }

    private void ScrollCountdownSelectorTo(double horizontalOffset)
    {
        double targetOffset = Math.Clamp(horizontalOffset, 0, CountdownSelectorRail.ScrollableWidth);
        CountdownSelectorRail.ChangeView(targetOffset, null, null, true);
    }

    private string GetCountdownSelectorText(CountdownItem countdown, int index)
    {
        return string.IsNullOrWhiteSpace(countdown.Title)
            ? FormatNewCountdownTitle(index + 1)
            : countdown.Title;
    }

    private string FormatNewCountdownTitle(int number)
    {
        string format = GetString("NewCountdownTitleFormat");
        return string.Format(CultureInfo.CurrentCulture, format, number);
    }

    private Brush GetToolbarToggleForeground(bool isActive, bool isEnabled)
    {
        string resourceKey = !isEnabled
            ? "TextFillColorDisabledBrush"
            : isActive
                ? "AccentFillColorDefaultBrush"
                : "TextFillColorPrimaryBrush";

        var fallbackColor = isEnabled ? Colors.White : Colors.Gray;
        return GetResourceBrush(resourceKey, new SolidColorBrush(fallbackColor));
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
        string fontFamily = _isWindows11OrGreater ? "Segoe Fluent Icons, Segoe MDL2 Assets" : "Segoe MDL2 Assets";
        string glyph = _isWindows11OrGreater ? WidgetAddGlyph : TileGlyph;
        GlanceSurfaceIcon.FontFamily = new FontFamily(fontFamily);
        GlanceSurfaceIcon.Glyph = glyph;
        GlanceSurfaceMenuIcon.FontFamily = new FontFamily(fontFamily);
        GlanceSurfaceMenuIcon.Glyph = glyph;
    }

    private void UpdateGlanceSurfaceStatus()
    {
        if (!_isWindows11OrGreater)
        {
            GlanceSurfaceIcon.Foreground = GetToolbarToggleForeground(_state.TileEnabled, TileButton.IsEnabled);
            string tileTooltip = GetString(_state.TileEnabled ? "TileButton/DisableTooltip" : "TileButton/EnableTooltip");
            ToolTipService.SetToolTip(TileButton, tileTooltip);
            AutomationProperties.SetName(TileButton, tileTooltip);
            AutomationProperties.SetHelpText(TileButton, GetString("TileButton/HelpText"));
            return;
        }

        GlanceSurfaceIcon.Foreground = GetToolbarToggleForeground(isActive: false, TileButton.IsEnabled);
        ToolTipService.SetToolTip(TileButton, GetString("WidgetButton/Tooltip"));
        AutomationProperties.SetName(TileButton, GetString("WidgetButton/Tooltip"));
        AutomationProperties.SetHelpText(TileButton, GetString("WidgetButton/HelpText"));
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

    private async Task<bool> ApplyEditorControlsToStateAsync(bool reconcileStartupTask)
    {
        CountdownDraft draft = CreateDraftFromControls();
        DateTimeOffset now = DateTimeOffset.Now;
        if (!draft.CanCommit(now))
        {
            UpdateEditorCommandButtons();
            return false;
        }

        if (!_state.HasCountdowns)
        {
            CountdownItem newCountdown = new(
                Guid.NewGuid().ToString("N"),
                draft.Title,
                draft.TargetDate!.Value,
                draft.ToastEnabled,
                draft.TargetTimeZoneId);
            CountdownState createdState = _state
                .AddCountdown(newCountdown, selectCountdown: true)
                .With(tileEnabled: draft.TileEnabled);
            await ApplyCommittedStateAsync(createdState, reconcileStartupTask);
            return true;
        }

        CountdownState stateToEdit = _state.SelectCountdown(_editingCountdownId);
        if (!draft.TryApplyTo(stateToEdit, now, out CountdownState? nextState) || nextState is null)
        {
            UpdateEditorCommandButtons();
            return false;
        }

        await ApplyCommittedStateAsync(nextState, reconcileStartupTask);
        return true;
    }

    private async Task ApplyCommittedStateAsync(
        CountdownState state,
        bool reconcileStartupTask,
        bool refreshEditor = false,
        bool updateButtonStatus = true)
    {
        _state = ApplyPreferencesToState(NormalizeStateForCurrentOs(state), DateTimeOffset.Now, out _);
        ReconcileDesktopOverlaySettingsWithState(saveIfChanged: true);
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

    private CountdownDraft CreateDraftFromControls()
    {
        string targetTimeZoneId = GetDraftTargetTimeZoneId();
        TimeSpan targetTime = EditorTimeOptionsButton.IsChecked == true
            ? TimePickerTargetTime.Time
            : CountdownTimeZone.DefaultDeadlineTimeOfDay;
        DateTimeOffset? targetDate = DatePickerTargetDate.Date.HasValue
            ? CountdownTimeZone.CreateTargetDate(
                DatePickerTargetDate.Date.Value.Date,
                targetTime,
                targetTimeZoneId)
            : null;

        return new CountdownDraft(
            TextBoxTitle.Text ?? string.Empty,
            targetDate,
            _isWindows11OrGreater ? false : _state.TileEnabled,
            _state.ToastEnabled,
            targetTimeZoneId);
    }

    private string GetDraftTargetTimeZoneId()
    {
        return EditorTimeZoneOptionsButton.IsChecked == true
            ? GetSelectedTargetTimeZoneId()
            : CountdownTimeZone.DefaultTimeZoneId;
    }

    private CountdownDraft CreateDraftFromState(bool? tileEnabled = null, bool? toastEnabled = null)
    {
        if (_state.SelectedCountdown is not CountdownItem selectedCountdown)
        {
            return new CountdownDraft(string.Empty, null, tileEnabled: false, toastEnabled: false);
        }

        return new CountdownDraft(
            selectedCountdown.Title,
            selectedCountdown.TargetDate,
            _isWindows11OrGreater ? false : tileEnabled ?? _state.TileEnabled,
            toastEnabled ?? selectedCountdown.ToastEnabled,
            selectedCountdown.TargetTimeZoneId);
    }

    private void UpdateEditorCommandButtons()
    {
        if (!_isEditorVisible || SaveEditorButton is null)
        {
            return;
        }

        bool canSaveDraft = CreateDraftFromControls().CanCommit(DateTimeOffset.Now);
        ApplyToolbarButtonState(new ToolbarButtonState(
            SaveEditorButton,
            IsVisible: true,
            IsEnabled: canSaveDraft,
            IsActive: canSaveDraft));
    }

    private void UpdateEditorPreviewFromControls()
    {
        if (!_isEditorVisible)
        {
            return;
        }

        CountdownDraft draft = CreateDraftFromControls();
        TextBlockTitle.Text = string.IsNullOrWhiteSpace(draft.Title)
            ? GetString("Title/PlaceholderText")
            : draft.Title.Trim();
        TextBlockDays.Text = draft.TargetDate.HasValue
            ? FormatCountdown(draft.TargetDate.Value)
            : string.Empty;
        if (draft.TargetDate.HasValue)
        {
            _previewTargetDate = draft.TargetDate.Value;
        }

        ApplyCountdownTextStyle(hasCountdownText: true);

        if (CountdownDisplayPanel.Visibility != Visibility.Visible)
        {
            CountdownDisplayPanel.Visibility = Visibility.Visible;
            CountdownDisplayPanel.Opacity = 1;
        }

        UpdateMainContentPanelPadding();
        UpdateSettingsPreview();
        UpdateDesktopCountdown();
    }

    private string GetSelectedTargetTimeZoneId()
    {
        return ComboBoxTargetTimeZone.SelectedItem is CountdownTimeZoneOption option
            ? option.Id
            : _state.SelectedCountdown?.TargetTimeZoneId ?? CountdownTimeZone.DefaultTimeZoneId;
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

    private bool ApplyDesktopCountdownPreference()
    {
        IReadOnlyList<DesktopCountdownContent> contents = CreateDesktopCountdownContents();
        if (contents.Count == 0)
        {
            _isAdjustingDesktopCountdown = false;
            _desktopCountdownService.Hide();
            return true;
        }

        if (!RefreshDesktopOverlayAvailability())
        {
            _isAdjustingDesktopCountdown = false;
            _desktopCountdownService.Hide();
            return false;
        }

        return _desktopCountdownService.ShowOrUpdate(contents);
    }

    private void UpdateDesktopCountdown()
    {
        IReadOnlyList<DesktopCountdownContent> contents = CreateDesktopCountdownContents();
        if (contents.Count > 0)
        {
            if (!RefreshDesktopOverlayAvailability())
            {
                _desktopCountdownService.Hide();
                return;
            }

            _desktopCountdownService.ShowOrUpdate(contents);
            return;
        }

        _desktopCountdownService.Hide();
    }

    private IReadOnlyList<DesktopCountdownContent> CreateDesktopCountdownContents()
    {
        if (!_preferences.ExperimentalFeaturesEnabled)
        {
            return Array.Empty<DesktopCountdownContent>();
        }

        CountdownDisplayText displayText = CreateDisplayText();
        DateTimeOffset now = DateTimeOffset.Now;
        Dictionary<string, DesktopOverlaySettings> overlays = _preferences.DesktopOverlays
            .Where(overlay => overlay.IsEnabled)
            .ToDictionary(overlay => overlay.CountdownId, StringComparer.Ordinal);
        List<DesktopCountdownContent> contents = new();
        foreach (CountdownItem countdown in _state.Countdowns)
        {
            if (!overlays.TryGetValue(countdown.Id, out DesktopOverlaySettings? overlay))
            {
                continue;
            }

            contents.Add(new DesktopCountdownContent(
                countdown.Id,
                displayText.FormatCountdown(countdown.TargetDate, now, _preferences, CultureInfo.CurrentCulture),
                displayText.FormatTitle(countdown.Title),
                _preferences.DaysTextSize,
                _preferences.TitleTextSize,
                overlay.PositionX,
                overlay.PositionY,
                overlay.Scale,
                overlay.Css));
        }

        return contents;
    }

    private DesktopOverlaySettings GetSelectedDesktopOverlay()
    {
        if (!_state.HasCountdowns)
        {
            return new DesktopOverlaySettings(CountdownItem.DefaultId);
        }

        return _preferences.DesktopOverlays.FirstOrDefault(overlay => string.Equals(overlay.CountdownId, _state.SelectedCountdownId, StringComparison.Ordinal)) ??
            new DesktopOverlaySettings(_state.SelectedCountdownId);
    }

    private bool HasEnabledDesktopOverlays()
    {
        if (!_preferences.ExperimentalFeaturesEnabled)
        {
            return false;
        }

        HashSet<string> countdownIds = new(_state.Countdowns.Select(countdown => countdown.Id), StringComparer.Ordinal);
        return _preferences.DesktopOverlays.Any(overlay => overlay.IsEnabled && countdownIds.Contains(overlay.CountdownId));
    }

    private void SaveDesktopOverlay(DesktopOverlaySettings overlay)
    {
        List<DesktopOverlaySettings> overlays = _preferences.DesktopOverlays
            .Where(existing => !string.Equals(existing.CountdownId, overlay.CountdownId, StringComparison.Ordinal))
            .ToList();
        if (overlay.ShouldPersist)
        {
            overlays.Add(overlay);
        }

        _preferences = _preferences.With(desktopOverlays: overlays);
        _settingsStore.SavePreferences(_preferences);
    }

    private void ShowStartupNotificationSavedToast(bool toastEnabled)
    {
        if (!_state.HasCountdowns)
        {
            return;
        }

        CountdownDisplayText displayText = CreateDisplayText();
        string countdownTitle = displayText.FormatTitle(_state.Title);
        string contentFormat = GetString(toastEnabled
            ? "ToastButton/EnabledSavedContentFormat"
            : "ToastButton/DisabledSavedContentFormat");

        _startupNotificationService.ShowStatusToast(
            GetString("ToastButton/SavedTitle"),
            string.Format(CultureInfo.CurrentCulture, contentFormat, countdownTitle));
    }

    private async void CountdownSelectorItem_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (ShouldSuppressCountdownSelectorTap() ||
            _isUpdatingControls ||
            sender is not FrameworkElement item ||
            item.Tag is not string countdownId ||
            string.Equals(countdownId, _state.SelectedCountdownId, StringComparison.Ordinal))
        {
            return;
        }

        await ApplyCommittedStateAsync(
            _state.SelectCountdown(countdownId),
            reconcileStartupTask: false,
            refreshEditor: true);
        if (_isEditorVisible)
        {
            _editingCountdownId = countdownId;
        }
    }

    private void CountdownSelectorItem_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDraggingCountdownSelector && sender is Border item)
        {
            SetCountdownSelectorItemVisual(item, isPointerOver: true);
        }
    }

    private void CountdownSelectorItem_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border item)
        {
            SetCountdownSelectorItemVisual(item, isPointerOver: false);
        }
    }

    private void CountdownSelectorItem_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border item)
        {
            SetCountdownSelectorItemVisual(item, isPointerOver: true, isPressed: true);
        }
    }

    private void CountdownSelectorItem_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border item)
        {
            SetCountdownSelectorItemVisual(item, isPointerOver: false);
        }
    }

    private void CountdownSelectorItem_PointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border item)
        {
            SetCountdownSelectorItemVisual(item, isPointerOver: false);
        }
    }

    private bool ShouldSuppressCountdownSelectorTap()
    {
        return _didDragCountdownSelector ||
            (DateTimeOffset.UtcNow - _lastCountdownSelectorDragAt).TotalMilliseconds < 250;
    }

    private void CountdownSelectorRail_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        if (CountdownSelectorRail.ScrollableWidth <= 0)
        {
            return;
        }

        int wheelDelta = e.GetCurrentPoint(CountdownSelectorRail).Properties.MouseWheelDelta;
        double targetOffset = Math.Clamp(
            CountdownSelectorRail.HorizontalOffset - wheelDelta,
            0,
            CountdownSelectorRail.ScrollableWidth);

        CountdownSelectorRail.ChangeView(targetOffset, null, null);
        e.Handled = true;
    }

    private void CountdownSelectorRail_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateCountdownSelectorContentWidth();
        BringSelectedCountdownIntoView();
    }

    private void CountdownSelectorRail_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (CountdownSelectorRail.ScrollableWidth <= 0)
        {
            return;
        }

        PointerPoint pointerPoint = e.GetCurrentPoint(CountdownSelectorRail);
        if (!pointerPoint.Properties.IsLeftButtonPressed)
        {
            return;
        }

        _isDraggingCountdownSelector = true;
        _didDragCountdownSelector = false;
        _countdownSelectorDragStartX = pointerPoint.Position.X;
        _countdownSelectorDragStartOffset = CountdownSelectorRail.HorizontalOffset;
    }

    private void CountdownSelectorRail_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDraggingCountdownSelector)
        {
            return;
        }

        PointerPoint pointerPoint = e.GetCurrentPoint(CountdownSelectorRail);
        if (!pointerPoint.Properties.IsLeftButtonPressed)
        {
            EndCountdownSelectorDrag(e);
            return;
        }

        double delta = pointerPoint.Position.X - _countdownSelectorDragStartX;
        if (Math.Abs(delta) < 3)
        {
            return;
        }

        _didDragCountdownSelector = true;
        ScrollCountdownSelectorTo(_countdownSelectorDragStartOffset - delta);
        e.Handled = true;
    }

    private void CountdownSelectorRail_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        EndCountdownSelectorDrag(e);
    }

    private void CountdownSelectorRail_PointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        EndCountdownSelectorDrag(e);
    }

    private void EndCountdownSelectorDrag(PointerRoutedEventArgs e)
    {
        if (!_isDraggingCountdownSelector)
        {
            return;
        }

        _isDraggingCountdownSelector = false;
        if (_didDragCountdownSelector)
        {
            _lastCountdownSelectorDragAt = DateTimeOffset.UtcNow;
            ResetCountdownSelectorItemVisuals();
        }
    }

    private void MainContentScroller_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateMainContentPanelPadding();
    }

    private void TextBoxTitle_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_isUpdatingControls)
        {
            UpdateEditorPreviewFromControls();
            UpdateEditorCommandButtons();
        }
    }

    private void DatePickerTargetDate_SelectedDateChanged(CalendarDatePicker sender, CalendarDatePickerDateChangedEventArgs args)
    {
        if (!_isUpdatingControls)
        {
            UpdateTargetDatePickerMinDate(GetDraftTargetTimeZoneId(), CreateDraftFromControls().TargetDate ?? GetSelectedOrDefaultTargetDate());
            UpdateEditorPreviewFromControls();
            UpdateEditorCommandButtons();
        }
    }

    private void TimePickerTargetTime_TimeChanged(object? sender, TimePickerValueChangedEventArgs e)
    {
        if (!_isUpdatingControls)
        {
            UpdateEditorPreviewFromControls();
            UpdateEditorCommandButtons();
        }
    }

    private void ComboBoxTargetTimeZone_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isUpdatingControls)
        {
            CountdownDraft draft = CreateDraftFromControls();
            UpdateTargetDatePickerMinDate(draft.TargetTimeZoneId, draft.TargetDate ?? GetSelectedOrDefaultTargetDate());
            UpdateEditorPreviewFromControls();
            UpdateEditorCommandButtons();
        }
    }

    private void EditorTimeOptionsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingControls)
        {
            return;
        }

        SetEditorOptionVisible(TimePickerTargetTime, EditorTimeOptionsButton.IsChecked == true);
        UpdateEditorOptionToggleVisuals();
        UpdateEditorPreviewFromControls();
        UpdateEditorCommandButtons();
    }

    private void EditorTimeZoneOptionsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingControls)
        {
            return;
        }

        SetEditorOptionVisible(ComboBoxTargetTimeZone, EditorTimeZoneOptionsButton.IsChecked == true);
        UpdateTargetDatePickerMinDate(GetDraftTargetTimeZoneId(), CreateDraftFromControls().TargetDate ?? GetSelectedOrDefaultTargetDate());
        UpdateEditorOptionToggleVisuals();
        UpdateEditorPreviewFromControls();
        UpdateEditorCommandButtons();
    }

    private async void TileButton_Click(object sender, RoutedEventArgs e)
    {
        await ToggleGlanceSurfaceAsync(TileButton);
    }

    private async Task ToggleGlanceSurfaceAsync(FrameworkElement placementTarget)
    {
        if (!_state.HasCountdowns)
        {
            UpdateButtonStatus();
            return;
        }

        if (_isWindows11OrGreater)
        {
            ShowWidgetHelpFlyout(placementTarget);
            return;
        }

        bool isPinned = await _startMenuService.IsPinnedAsync();
        if (_state.TileEnabled && !isPinned)
        {
            await ApplyCommittedStateAsync(_state.With(tileEnabled: false), reconcileStartupTask: true);
        }

        bool tileEnabled = !_state.TileEnabled;
        if (!tileEnabled)
        {
            await ApplyCommittedStateAsync(_state.With(tileEnabled: false), reconcileStartupTask: true);
            return;
        }

        if (!isPinned)
        {
            bool pinned = await _startMenuService.RequestPinAsync();
            if (!pinned)
            {
                UpdateButtonStatus();
                return;
            }
        }

        CountdownDraft draft = CreateDraftFromState(tileEnabled: true);
        if (!draft.TryApplyTo(_state, DateTimeOffset.Now, out CountdownState? nextState) || nextState is null)
        {
            UpdateButtonStatus();
            return;
        }

        await TryApplyStartupBackedStateAsync(nextState);
    }

    private void ShowWidgetHelpFlyout(FrameworkElement placementTarget)
    {
        _widgetHelpFlyout ??= CreateWidgetHelpFlyout();
        _widgetHelpFlyout.ShowAt(placementTarget);
    }

    private Flyout CreateWidgetHelpFlyout()
    {
        StackPanel content = new StackPanel
        {
            MinWidth = 260,
            MaxWidth = 320,
            Spacing = 10
        };

        content.Children.Add(new TextBlock
        {
            Text = GetString("WidgetFlyout/Title"),
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });

        StackPanel steps = new StackPanel
        {
            Spacing = 6
        };
        steps.Children.Add(CreateFlyoutStep(GetString("WidgetFlyout/StepOpen")));
        steps.Children.Add(CreateFlyoutStep(GetString("WidgetFlyout/StepChoose")));
        steps.Children.Add(CreateFlyoutStep(GetString("WidgetFlyout/StepPin")));
        content.Children.Add(steps);

        Button okButton = new Button
        {
            Content = GetString("WidgetFlyout/OkButton"),
            HorizontalAlignment = HorizontalAlignment.Right
        };
        okButton.Click += WidgetFlyoutOkButton_Click;
        content.Children.Add(okButton);

        return new Flyout
        {
            Placement = Microsoft.UI.Xaml.Controls.Primitives.FlyoutPlacementMode.Top,
            Content = content
        };
    }

    private static FrameworkElement CreateFlyoutStep(string text)
    {
        StackPanel row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };

        row.Children.Add(new TextBlock
        {
            Text = "-",
            Opacity = 0.78
        });
        row.Children.Add(new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 280,
            Opacity = 0.78
        });

        return row;
    }

    private async void WidgetFlyoutOkButton_Click(object sender, RoutedEventArgs e)
    {
        _widgetHelpFlyout?.Hide();
        await Launcher.LaunchUriAsync(new Uri("ms-widgetboard:"));
    }

    private async void NotifyButton_Click(object sender, RoutedEventArgs e)
    {
        await ToggleStartupNotificationAsync();
    }

    private async Task ToggleStartupNotificationAsync()
    {
        if (!_state.HasCountdowns)
        {
            UpdateButtonStatus();
            return;
        }

        bool toastEnabled = !_state.ToastEnabled;
        if (!toastEnabled)
        {
            await ApplyCommittedStateAsync(_state.With(toastEnabled: false), reconcileStartupTask: true);
            ShowStartupNotificationSavedToast(toastEnabled);
            return;
        }

        CountdownDraft draft = CreateDraftFromState(toastEnabled: true);
        if (!draft.TryApplyTo(_state, DateTimeOffset.Now, out CountdownState? nextState) || nextState is null)
        {
            UpdateButtonStatus();
            return;
        }

        if (await TryApplyStartupBackedStateAsync(nextState))
        {
            ShowStartupNotificationSavedToast(toastEnabled);
        }
    }

    private async void AddButton_Click(object sender, RoutedEventArgs e)
    {
        await AddCountdownAsync();
    }

    private async Task AddCountdownAsync()
    {
        if (_isEditorVisible)
        {
            return;
        }

        CountdownState nextState = _state.AddDefaultCountdown(
            CreateDefaultTargetDate(),
            FormatNewCountdownTitle);
        await ApplyCommittedStateAsync(nextState, reconcileStartupTask: true);
    }

    private void EditButton_Click(object sender, RoutedEventArgs e)
    {
        StartEditingSelectedCountdown();
    }

    private void StartEditingSelectedCountdown()
    {
        if (_isEditorVisible || _state.SelectedCountdown is not CountdownItem selectedCountdown)
        {
            return;
        }

        ShowInlineEditor(selectedCountdown);
    }

    private void CancelEditorButton_Click(object sender, RoutedEventArgs e)
    {
        ExitEditorMode(restorePreview: true);
    }

    private async void SaveEditorButton_Click(object sender, RoutedEventArgs e)
    {
        await SaveEditorAsync();
    }

    private async Task SaveEditorAsync()
    {
        if (await ApplyEditorControlsToStateAsync(reconcileStartupTask: true))
        {
            ExitEditorMode(restorePreview: false);
        }
    }

    private async void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        await DeleteSelectedCountdownAsync();
    }

    private async Task DeleteSelectedCountdownAsync()
    {
        if (!_state.CanRemoveCountdown)
        {
            UpdateButtonStatus();
            return;
        }

        string removedCountdownId = _state.SelectedCountdownId;
        bool shouldExitEditor = _isEditorVisible &&
            string.Equals(_editingCountdownId, removedCountdownId, StringComparison.Ordinal);
        if (shouldExitEditor)
        {
            ExitEditorMode(restorePreview: false);
        }

        await AnimateCountdownSelectorItemRemovalAsync(removedCountdownId);
        await ApplyCommittedStateAsync(
            _state.RemoveCountdown(removedCountdownId),
            reconcileStartupTask: true,
            refreshEditor: _isEditorVisible);
    }

    private async void NotifyMenuItem_Click(object sender, RoutedEventArgs e)
    {
        await ToggleStartupNotificationAsync();
    }

    private async void TileMenuItem_Click(object sender, RoutedEventArgs e)
    {
        await ToggleGlanceSurfaceAsync(MoreButton);
    }

    private async void AddMenuItem_Click(object sender, RoutedEventArgs e)
    {
        await AddCountdownAsync();
    }

    private void EditMenuItem_Click(object sender, RoutedEventArgs e)
    {
        StartEditingSelectedCountdown();
    }

    private async void DeleteMenuItem_Click(object sender, RoutedEventArgs e)
    {
        await DeleteSelectedCountdownAsync();
    }

    private void DesktopCountdownButton_Click(object sender, RoutedEventArgs e)
    {
        OpenDesktopCountdownPage();
    }

    private void SettingsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        ShowSettingsPage();
    }

    private void DesktopCountdownMenuItem_Click(object sender, RoutedEventArgs e)
    {
        OpenDesktopCountdownPage();
    }

    private void OpenDesktopCountdownPage()
    {
        if (!_state.HasCountdowns || !_preferences.ExperimentalFeaturesEnabled)
        {
            UpdateButtonStatus();
            return;
        }

        if (!RefreshDesktopOverlayAvailability())
        {
            _startupNotificationService.ShowStatusToast(
                GetString("Settings/DesktopCountdownFailedTitle"),
                GetString("Settings/DesktopCountdownFailedContent"));
            return;
        }

        ShowDesktopCountdownPage();
    }

    private void SettingsBackButton_Click(object sender, RoutedEventArgs e)
    {
        ShowCountdownPage();
    }

    private void ToolbarLayoutEntryButton_Click(object sender, RoutedEventArgs e)
    {
        ShowToolbarLayoutPage();
    }

    private void ToolbarLayoutBackButton_Click(object sender, RoutedEventArgs e)
    {
        ShowSettingsPage();
    }

    private void DesktopCountdownBackButton_Click(object sender, RoutedEventArgs e)
    {
        ShowCountdownPage();
    }

    private void ShowSettingsPage()
    {
        _widgetHelpFlyout?.Hide();
        ExitEditorMode(restorePreview: true);
        _isSettingsVisible = true;
        _isDesktopCountdownPageVisible = false;
        _isToolbarLayoutPageVisible = false;
        StopDesktopCountdownAdjustment();
        HideCountdownSelectorImmediately(clearItems: true);
        MainContentScroller.Visibility = Visibility.Collapsed;
        DesktopCountdownContentScroller.Visibility = Visibility.Collapsed;
        ToolbarLayoutContentScroller.Visibility = Visibility.Collapsed;
        SettingsContentScroller.Visibility = Visibility.Visible;
        SettingsContentScroller.ChangeView(null, 0, null, true);
        UpdateSettingsControls();
    }

    private void ShowToolbarLayoutPage()
    {
        _widgetHelpFlyout?.Hide();
        ExitEditorMode(restorePreview: true);
        _isSettingsVisible = true;
        _isDesktopCountdownPageVisible = false;
        _isToolbarLayoutPageVisible = true;
        StopDesktopCountdownAdjustment();
        HideCountdownSelectorImmediately(clearItems: true);
        MainContentScroller.Visibility = Visibility.Collapsed;
        SettingsContentScroller.Visibility = Visibility.Collapsed;
        DesktopCountdownContentScroller.Visibility = Visibility.Collapsed;
        ToolbarLayoutContentScroller.Visibility = Visibility.Visible;
        ToolbarLayoutContentScroller.ChangeView(null, 0, null, true);
        UpdateSettingsControls();
    }

    private void ShowDesktopCountdownPage()
    {
        if (!_state.HasCountdowns)
        {
            ShowCountdownPage();
            return;
        }

        if (!_preferences.ExperimentalFeaturesEnabled)
        {
            ShowSettingsPage();
            return;
        }

        if (!RefreshDesktopOverlayAvailability())
        {
            return;
        }

        _widgetHelpFlyout?.Hide();
        ExitEditorMode(restorePreview: true);
        _isSettingsVisible = false;
        _isDesktopCountdownPageVisible = true;
        _isToolbarLayoutPageVisible = false;
        HideCountdownSelectorImmediately(clearItems: true);
        MainContentScroller.Visibility = Visibility.Collapsed;
        SettingsContentScroller.Visibility = Visibility.Collapsed;
        ToolbarLayoutContentScroller.Visibility = Visibility.Collapsed;
        DesktopCountdownContentScroller.Visibility = Visibility.Visible;
        DesktopCountdownContentScroller.ChangeView(null, 0, null, true);
        UpdateSettingsControls();
    }

    private void ShowInlineEditor(CountdownItem countdown)
    {
        _widgetHelpFlyout?.Hide();
        StopDesktopCountdownAdjustment();
        _isSettingsVisible = false;
        _isDesktopCountdownPageVisible = false;
        _isToolbarLayoutPageVisible = false;
        _isEditorVisible = true;
        _editingCountdownId = countdown.Id;
        SettingsContentScroller.Visibility = Visibility.Collapsed;
        DesktopCountdownContentScroller.Visibility = Visibility.Collapsed;
        ToolbarLayoutContentScroller.Visibility = Visibility.Collapsed;
        MainContentScroller.Visibility = Visibility.Visible;

        _isUpdatingControls = true;
        try
        {
            UpdateEditorFromCountdown(countdown);
        }
        finally
        {
            _isUpdatingControls = false;
        }

        SetInlineEditorVisible(true);
        UpdateEditorCommandButtons();
        UpdateCountdownSelector();
        UpdateButtonStatus();
        TextBoxTitle.Focus(FocusState.Programmatic);
        TextBoxTitle.Select(TextBoxTitle.Text.Length, 0);
    }

    private void ExitEditorMode(bool restorePreview)
    {
        bool wasEditorVisible = _isEditorVisible;
        _isEditorVisible = false;
        _editingCountdownId = string.Empty;
        SetInlineEditorVisible(false);

        if (restorePreview)
        {
            UpdateCountdownPreview();
        }

        if (wasEditorVisible)
        {
            UpdateCountdownSelector();
        }

        UpdateButtonStatus();
    }

    private void SetInlineEditorVisible(bool isVisible)
    {
        _inlineEditorVisibilityStoryboard?.Stop();
        _inlineEditorVisibilityStoryboard = null;

        if (!_isMotionReady || !AnimationsEnabled)
        {
            InlineEditorPanel.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
            InlineEditorPanel.Opacity = isVisible ? 1 : 0;
            EnsureInlineEditorTranslateTransform().Y = 0;
            UpdateMainContentPanelPadding();
            return;
        }

        TranslateTransform transform = EnsureInlineEditorTranslateTransform();
        if (isVisible)
        {
            InlineEditorPanel.Visibility = Visibility.Visible;
            InlineEditorPanel.Opacity = 0;
            transform.Y = InlineEditorEntranceOffset;
            InlineEditorPanel.UpdateLayout();
            UpdateMainContentPanelPadding();
            Storyboard showStoryboard = new();
            showStoryboard.Children.Add(CreateOpacityAnimation(InlineEditorPanel, 1));
            showStoryboard.Children.Add(CreateTranslateYAnimation(transform, 0));
            _inlineEditorVisibilityStoryboard = showStoryboard;
            showStoryboard.Completed += (_, _) =>
            {
                if (ReferenceEquals(_inlineEditorVisibilityStoryboard, showStoryboard))
                {
                    _inlineEditorVisibilityStoryboard = null;
                    InlineEditorPanel.Opacity = 1;
                    transform.Y = 0;
                    UpdateMainContentPanelPadding();
                }
            };
            showStoryboard.Begin();
            return;
        }

        InlineEditorPanel.Visibility = Visibility.Collapsed;
        InlineEditorPanel.Opacity = 0;
        transform.Y = 0;
        UpdateMainContentPanelPadding();
    }

    private TranslateTransform EnsureInlineEditorTranslateTransform()
    {
        if (InlineEditorPanel.RenderTransform is TranslateTransform transform)
        {
            return transform;
        }

        TranslateTransform newTransform = new();
        InlineEditorPanel.RenderTransform = newTransform;
        return newTransform;
    }

    private void SetEditorOptionVisible(FrameworkElement element, bool isVisible, bool animate = true)
    {
        element.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
        element.Opacity = isVisible ? 1 : 0;
        UpdateMainContentPanelPadding();
    }

    private void ShowCountdownPage()
    {
        StopDesktopCountdownAdjustment();
        ExitEditorMode(restorePreview: true);
        _isSettingsVisible = false;
        _isDesktopCountdownPageVisible = false;
        _isToolbarLayoutPageVisible = false;
        SettingsContentScroller.Visibility = Visibility.Collapsed;
        DesktopCountdownContentScroller.Visibility = Visibility.Collapsed;
        ToolbarLayoutContentScroller.Visibility = Visibility.Collapsed;
        MainContentScroller.Visibility = Visibility.Visible;
        _isUpdatingControls = true;
        try
        {
            UpdateEditorFromState();
        }
        finally
        {
            _isUpdatingControls = false;
        }

        UpdateButtonStatus();
    }

    private async void SortCountdownsToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingSettingsControls)
        {
            return;
        }

        _preferences = _preferences.With(sortCountdownsByDaysLeft: SortCountdownsToggle.IsOn);
        _settingsStore.SavePreferences(_preferences);
        _state = ApplyPreferencesToState(_state);
        ReconcileDesktopOverlaySettingsWithState(saveIfChanged: true);
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

        SyncWidgets();
        if (!_isWindows11OrGreater)
        {
            await UpdateLiveTileFromStateAsync();
        }

        UpdateButtonStatus();
    }

    private async void AutoDeleteExpiredCountdownsToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingSettingsControls)
        {
            return;
        }

        _preferences = _preferences.With(autoDeleteExpiredCountdowns: AutoDeleteExpiredCountdownsToggle.IsOn);
        _settingsStore.SavePreferences(_preferences);
        await ApplyCommittedStateAsync(_state, reconcileStartupTask: true, refreshEditor: true);
        UpdateSettingsControls();
    }

    private void ToolbarLayoutListView_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
    {
        if (e.Items.OfType<ListViewItem>().Any(item => ReferenceEquals(item.Tag, ToolbarLayoutDividerTag)))
        {
            e.Cancel = true;
        }
    }

    private void ToolbarLayoutListView_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
    {
        if (_isUpdatingSettingsControls)
        {
            return;
        }

        SaveToolbarLayoutFromListView();
    }

    private void SaveToolbarLayoutFromListView()
    {
        List<CountdownToolbarAction?> actions = [];
        int primaryActionCount = CountdownToolbarLayout.Default.PrimaryActionCount;
        bool foundDivider = false;

        foreach (object itemObject in ToolbarLayoutListView.Items)
        {
            if (itemObject is not ListViewItem item)
            {
                continue;
            }

            if (ReferenceEquals(item.Tag, ToolbarLayoutDividerTag))
            {
                primaryActionCount = actions.Count;
                foundDivider = true;
                continue;
            }

            if (item.Tag is CountdownToolbarAction action)
            {
                actions.Add(action);
            }
        }

        if (!foundDivider)
        {
            primaryActionCount = Math.Min(primaryActionCount, actions.Count);
        }

        _preferences = _preferences.With(toolbarLayout: new CountdownToolbarLayout(actions, primaryActionCount));
        _settingsStore.SavePreferences(_preferences);
        UpdateSettingsControls();
        UpdateButtonStatus();
    }

    private void DaysTextSizeSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_isUpdatingSettingsControls)
        {
            return;
        }

        _preferences = _preferences.With(daysTextSize: e.NewValue);
        _settingsStore.SavePreferences(_preferences);
        ApplyDisplaySizePreference();
    }

    private void TitleTextSizeSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_isUpdatingSettingsControls)
        {
            return;
        }

        _preferences = _preferences.With(titleTextSize: e.NewValue);
        _settingsStore.SavePreferences(_preferences);
        ApplyDisplaySizePreference();
    }

    private void HourDisplayModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingSettingsControls)
        {
            return;
        }

        if (HourDisplayModeComboBox.SelectedItem is not HourDisplayModeOption option)
        {
            return;
        }

        _preferences = _preferences.With(hourDisplayMode: option.Mode);
        _settingsStore.SavePreferences(_preferences);
        ScheduleNextDateRefresh();
        RefreshDateDependentSurfaces();
        UpdateSettingsControls();
    }

    private void HoursDisplayThresholdDaysBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_isUpdatingSettingsControls)
        {
            return;
        }

        int thresholdDays = double.IsNaN(args.NewValue)
            ? CountdownPreferences.DefaultHoursDisplayThresholdDays
            : (int)Math.Round(args.NewValue);
        _preferences = _preferences.With(hoursDisplayThresholdDays: thresholdDays);
        _settingsStore.SavePreferences(_preferences);
        RefreshDateDependentSurfaces();
        UpdateSettingsControls();
    }

    private void ReduceMotionToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingSettingsControls)
        {
            return;
        }

        _preferences = _preferences.With(reduceMotion: ReduceMotionToggle.IsOn);
        _settingsStore.SavePreferences(_preferences);
        ApplyMotionPreference();
        UpdateSettingsControls();
    }

    private async void OpenAtStartupToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingSettingsControls)
        {
            return;
        }

        CountdownPreferences nextPreferences = _preferences.With(openWindowAtStartup: OpenAtStartupToggle.IsOn);
        if (nextPreferences.OpenWindowAtStartup &&
            !await _startupFeatureService.EnsureAvailableForAsync(_state, nextPreferences))
        {
            _startupNotificationService.ShowStatusToast(
                GetString("CreateStartupTaskFailedNotification/Title"),
                GetString("CreateStartupTaskFailedNotification/Content"));
            UpdateSettingsControls();
            return;
        }

        _preferences = nextPreferences;
        _settingsStore.SavePreferences(_preferences);
        await ReconcileStartupTaskAsync();
        UpdateSettingsControls();
    }

    private void ExperimentalFeaturesToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingSettingsControls)
        {
            return;
        }

        _preferences = _preferences.With(experimentalFeaturesEnabled: ExperimentalFeaturesToggle.IsOn);
        _settingsStore.SavePreferences(_preferences);
        if (!_preferences.ExperimentalFeaturesEnabled)
        {
            StopDesktopCountdownAdjustment(showDesktopCountdown: false);
            _desktopCountdownService.Hide();
        }
        else
        {
            RefreshDesktopOverlayAvailability();
        }

        UpdateSettingsControls();
        UpdateButtonStatus();
    }

    private void DesktopCountdownToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingSettingsControls || !_state.HasCountdowns || !_preferences.ExperimentalFeaturesEnabled)
        {
            return;
        }

        DesktopOverlaySettings nextOverlay = GetSelectedDesktopOverlay().With(isEnabled: DesktopCountdownToggle.IsOn);
        SaveDesktopOverlay(nextOverlay);
        if (!nextOverlay.IsEnabled)
        {
            StopDesktopCountdownAdjustment(showDesktopCountdown: false);
        }

        if (!ApplyDesktopCountdownPreference())
        {
            SaveDesktopOverlay(nextOverlay.With(isEnabled: false));
            _startupNotificationService.ShowStatusToast(
                GetString("Settings/DesktopCountdownFailedTitle"),
                GetString("Settings/DesktopCountdownFailedContent"));
            UpdateSettingsControls();
            return;
        }

        _settingsStore.SavePreferences(_preferences);
        UpdateSettingsControls();
        UpdateButtonStatus();
    }

    private void AdjustDesktopCountdownButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_state.HasCountdowns)
        {
            UpdateSettingsControls();
            return;
        }

        DesktopOverlaySettings selectedOverlay = GetSelectedDesktopOverlay();
        if (!selectedOverlay.IsEnabled)
        {
            UpdateSettingsControls();
            return;
        }

        if (_isAdjustingDesktopCountdown)
        {
            StopDesktopCountdownAdjustment();
            UpdateSettingsControls();
            return;
        }

        _isAdjustingDesktopCountdown = true;
        if (!_desktopCountdownService.StartAdjusting(CreateDesktopCountdownContents(), _state.SelectedCountdownId, SaveDesktopCountdownPlacement))
        {
            _isAdjustingDesktopCountdown = false;
            _startupNotificationService.ShowStatusToast(
                GetString("Settings/DesktopCountdownFailedTitle"),
                GetString("Settings/DesktopCountdownFailedContent"));
        }

        UpdateSettingsControls();
    }

    private void ResetDesktopCountdownPositionButton_Click(object sender, RoutedEventArgs e)
    {
        DesktopOverlaySettings overlay = GetSelectedDesktopOverlay().With(
            positionX: CountdownPreferences.DefaultDesktopCountdownX,
            positionY: CountdownPreferences.DefaultDesktopCountdownY,
            scale: CountdownPreferences.DefaultDesktopCountdownScale);
        SaveDesktopOverlay(overlay);
        UpdateDesktopCountdown();
        UpdateSettingsControls();
    }

    private void DesktopCountdownCssTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingSettingsControls)
        {
            return;
        }

        SaveDesktopOverlay(GetSelectedDesktopOverlay().With(css: DesktopCountdownCssTextBox.Text));
        UpdateDesktopCountdown();
    }

    private void DesktopCountdownStyleTemplateButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string css })
        {
            return;
        }

        if (!string.Equals(DesktopCountdownCssTextBox.Text, css, StringComparison.Ordinal))
        {
            DesktopCountdownCssTextBox.Text = css;
        }

        DesktopCountdownCssTextBox.Focus(FocusState.Programmatic);
        DesktopCountdownCssTextBox.Select(DesktopCountdownCssTextBox.Text.Length, 0);
    }

    private void ResetDesktopCountdownStyleButton_Click(object sender, RoutedEventArgs e)
    {
        SaveDesktopOverlay(GetSelectedDesktopOverlay().With(css: CountdownPreferences.DefaultDesktopCountdownCss));
        UpdateSettingsControls();
        UpdateDesktopCountdown();
    }

    private void SaveDesktopCountdownPlacement(double x, double y, double scale)
    {
        SaveDesktopOverlay(GetSelectedDesktopOverlay().With(positionX: x, positionY: y, scale: scale));
    }

    private void StopDesktopCountdownAdjustment(bool showDesktopCountdown = true)
    {
        if (!_isAdjustingDesktopCountdown)
        {
            return;
        }

        _isAdjustingDesktopCountdown = false;
        _desktopCountdownService.StopAdjusting();
        if (showDesktopCountdown)
        {
            UpdateDesktopCountdown();
        }
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        StopDateRefreshTimer();
        _desktopTrayService.Dispose();
        _desktopCountdownService.Dispose();
    }

    private void ExitFromTray()
    {
        _isExiting = true;
        StopDateRefreshTimer();
        _desktopTrayService.Dispose();
        _desktopCountdownService.Dispose();
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
