using DateCountdown.Core;
using DateCountdown.Services;
using DateCountdown.Windowing;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.Windows.ApplicationModel.Resources;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.System;

namespace DateCountdown;

public sealed partial class MainWindow : Window
{
    private const string TileGlyph = "\uECA5";
    private const string WidgetAddGlyph = "\uF036";
    private const double MainContentNavigationOffset = 34;
    private static readonly Duration VisibilityMotionDuration = new(TimeSpan.FromMilliseconds(167));

    private readonly bool _animationsEnabled = new Windows.UI.ViewManagement.UISettings().AnimationsEnabled;
    private readonly bool _isWindows11OrGreater = OperatingSystemInfo.IsWindows11OrGreater();
    private readonly CountdownSettingsStore _settingsStore = new();
    private readonly ResourceLoader _resourceLoader = new();
    private readonly StartMenuService _startMenuService = new();
    private readonly StartupFeatureService _startupFeatureService;
    private readonly StartupNotificationService _startupNotificationService = new();
    private readonly WidgetSyncService _widgetSyncService;
    private readonly WindowChromeService _windowChromeService = new();

    private CountdownState _state = CountdownState.CreateDefault(DateTimeOffset.Now);
    private CountdownPreferences _preferences = CountdownPreferences.Default;
    private int _dateDifference;
    private double _countdownSelectorDragStartOffset;
    private double _countdownSelectorDragStartX;
    private bool _didDragCountdownSelector;
    private bool _isDraggingCountdownSelector;
    private bool _isMotionReady;
    private bool _isSettingsVisible;
    private bool _isUpdatingControls;
    private bool _isUpdatingSettingsControls = true;
    private DateTimeOffset _lastCountdownSelectorDragAt = DateTimeOffset.MinValue;
    private Storyboard? _mainContentOffsetStoryboard;
    private Storyboard? _countdownSelectorVisibilityStoryboard;
    private Storyboard? _toolbarPositionStoryboard;
    private Flyout? _widgetHelpFlyout;

    public MainWindow()
    {
        _widgetSyncService = new WidgetSyncService(_isWindows11OrGreater);
        _startupFeatureService = new StartupFeatureService(!_isWindows11OrGreater, _startupNotificationService);

        InitializeComponent();
        _isUpdatingSettingsControls = false;

        _windowChromeService.Initialize(this, AppTitleBar, GetString("AppName"), _isWindows11OrGreater);
        Activated += MainWindow_Activated;

        LoadPreferences();
        LoadState();
        SetDisplay();
        _isMotionReady = true;
        SyncWidgets();
        _ = ReconcileStartupTaskAsync();
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

    private void LoadPreferences()
    {
        _preferences = _settingsStore.LoadPreferences();
    }

    private void LoadState()
    {
        _state = ApplyPreferencesToState(NormalizeStateForCurrentOs(_settingsStore.Load(DateTimeOffset.Now)));
    }

    private CountdownState NormalizeStateForCurrentOs(CountdownState state)
    {
        return _startupFeatureService.NormalizeState(state);
    }

    private CountdownState ApplyPreferencesToState(CountdownState state)
    {
        return _preferences.SortCountdownsByDaysLeft
            ? state.SortByDaysLeft(DateTimeOffset.Now)
            : state;
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
        string deleteTooltip = GetString("DeleteCountdownButton/Tooltip");
        string moreTooltip = GetString("MoreButton/Tooltip");

        ConfigureGlanceSurfaceButton();
        ToolTipService.SetToolTip(NotifyButton, toastTooltip);
        ToolTipService.SetToolTip(AddButton, addTooltip);
        ToolTipService.SetToolTip(DeleteButton, deleteTooltip);
        ToolTipService.SetToolTip(MoreButton, moreTooltip);
        AutomationProperties.SetName(NotifyButton, toastTooltip);
        AutomationProperties.SetName(AddButton, addTooltip);
        AutomationProperties.SetName(DeleteButton, deleteTooltip);
        AutomationProperties.SetName(MoreButton, moreTooltip);
        AutomationProperties.SetHelpText(NotifyButton, GetString("ToastButton/HelpText"));
        AutomationProperties.SetHelpText(AddButton, GetString("AddCountdownButton/HelpText"));
        AutomationProperties.SetHelpText(DeleteButton, GetString("DeleteCountdownButton/HelpText"));
        AutomationProperties.SetHelpText(MoreButton, GetString("MoreButton/HelpText"));
        AutomationProperties.SetName(CountdownSelectorRail, GetString("CountdownSelector/Name"));
        AutomationProperties.SetName(DatePickerTargetDate, GetString("TargetDatePicker/Name"));
        AutomationProperties.SetName(TextBoxTitle, GetString("Title/PlaceholderText"));

        ConfigureSettingsText();
        UpdateSettingsControls();
        ApplyDisplaySizePreference();
        UpdateButtonStatus();
    }

    private void ConfigureSettingsText()
    {
        AutomationProperties.SetName(SettingsBackButton, GetString("Settings/BackButton"));
        ToolTipService.SetToolTip(SettingsBackButton, GetString("Settings/BackButton"));
        SettingsTitleText.Text = GetString("Settings/Title");
        SettingsCountdownsHeaderText.Text = GetString("Settings/CountdownsHeader");
        SettingsAppearanceHeaderText.Text = GetString("Settings/AppearanceHeader");
        SettingsStartupHeaderText.Text = GetString("Settings/StartupHeader");
        SettingsAboutHeaderText.Text = GetString("Settings/AboutHeader");

        SettingsMenuItem.Text = GetString("SettingsMenuItem/Text");
        SortCountdownsTitleText.Text = GetString("Settings/SortCountdownsTitle");
        TextSizeTitleText.Text = GetString("Settings/TextSizeTitle");
        DaysTextSizeLabelText.Text = GetString("Settings/DaysTextSizeLabel");
        TitleTextSizeLabelText.Text = GetString("Settings/TitleTextSizeLabel");
        OpenAtStartupTitleText.Text = GetString("Settings/OpenAtStartupTitle");
        AboutAppNameText.Text = GetString("AppName");
        AboutVersionText.Text = GetAppVersion();
        SourceCodeLinkButton.Content = GetString("Settings/SourceCodeLink");

        AutomationProperties.SetName(SortCountdownsToggle, SortCountdownsTitleText.Text);
        AutomationProperties.SetHelpText(SortCountdownsToggle, GetString("Settings/SortCountdownsDescription"));
        AutomationProperties.SetName(TextSizeExpander, TextSizeTitleText.Text);
        AutomationProperties.SetHelpText(TextSizeExpander, GetString("Settings/TextSizeDescription"));
        AutomationProperties.SetName(DaysTextSizeSlider, DaysTextSizeLabelText.Text);
        AutomationProperties.SetName(TitleTextSizeSlider, TitleTextSizeLabelText.Text);
        AutomationProperties.SetName(OpenAtStartupToggle, OpenAtStartupTitleText.Text);
        AutomationProperties.SetHelpText(OpenAtStartupToggle, GetString("Settings/OpenAtStartupDescription"));
        AutomationProperties.SetName(SourceCodeLinkButton, GetString("Settings/SourceCodeLink"));

        DaysTextSizeSlider.Minimum = CountdownPreferences.MinDaysTextSize;
        DaysTextSizeSlider.Maximum = CountdownPreferences.MaxDaysTextSize;
        TitleTextSizeSlider.Minimum = CountdownPreferences.MinTitleTextSize;
        TitleTextSizeSlider.Maximum = CountdownPreferences.MaxTitleTextSize;
    }

    private void UpdateSettingsControls()
    {
        _isUpdatingSettingsControls = true;
        try
        {
            SortCountdownsToggle.IsOn = _preferences.SortCountdownsByDaysLeft;
            OpenAtStartupToggle.IsOn = _preferences.OpenWindowAtStartup;
            DaysTextSizeSlider.Value = _preferences.DaysTextSize;
            TitleTextSizeSlider.Value = _preferences.TitleTextSize;
            UpdateTextSizeValueText();
        }
        finally
        {
            _isUpdatingSettingsControls = false;
        }
    }

    private void SettingsContentScroller_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        SettingsContentPanel.Width = Math.Min(640, Math.Max(360, e.NewSize.Width - 36));
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
        TextBlockDays.FontSize = _preferences.DaysTextSize;
        TextBlockTitle.FontSize = _preferences.TitleTextSize;
        MainContentPanel.MaxWidth = Math.Clamp(_preferences.DaysTextSize * 5.4, 360, 560);
        MainContentPanel.Spacing = Math.Clamp(_preferences.TitleTextSize + 12, 24, 34);
        UpdateSettingsPreview();
        UpdateTextSizeValueText();
    }

    private void UpdateEditorFromState()
    {
        UpdateCountdownPreview();
        UpdateCountdownSelector();
        TextBoxTitle.PlaceholderText = GetString("Title/PlaceholderText");
        TextBoxTitle.MaxLength = CountdownItem.MaxTitleLength;
        TextBoxTitle.Text = _state.Title;
        DateTimeOffset today = DateTimeOffset.Now.Date;
        DatePickerTargetDate.MinDate = _state.TargetDate.Date < today.Date
            ? _state.TargetDate.Date
            : today;
        DatePickerTargetDate.Date = _state.TargetDate;
    }

    private void UpdateCountdownPreview()
    {
        _dateDifference = CountdownLogic.CalculateDaysLeft(_state.TargetDate, DateTimeOffset.Now);
        TextBlockTitle.Text = _state.Title;
        TextBlockDays.Text = FormatDaysLeft(_dateDifference);
        UpdateSettingsPreview();
    }

    private void UpdateCountdownPreview(CountdownDraft draft)
    {
        DateTimeOffset targetDate = draft.TargetDate ?? _state.TargetDate;
        _dateDifference = CountdownLogic.CalculateDaysLeft(targetDate, DateTimeOffset.Now);
        TextBlockTitle.Text = draft.Title;
        TextBlockDays.Text = FormatDaysLeft(_dateDifference);
        UpdateSettingsPreview();
    }

    private void UpdateSettingsPreview()
    {
        if (SettingsPreviewDaysText is null || SettingsPreviewTitleText is null)
        {
            return;
        }

        SettingsPreviewDaysText.Text = FormatDaysLeft(_dateDifference);
        SettingsPreviewDaysText.FontSize = _preferences.DaysTextSize;
        SettingsPreviewTitleText.Text = string.IsNullOrWhiteSpace(_state.Title)
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
        bool canCommitDraft = CreateDraftFromControls().CanCommit(DateTimeOffset.Now);
        NotifyButton.IsEnabled = _state.ToastEnabled || canCommitDraft;
        TileButton.IsEnabled = _isWindows11OrGreater || _state.TileEnabled || canCommitDraft;
        AddButton.IsEnabled = true;
        SetDeleteButtonVisible(_state.CanRemoveCountdown);
        NotifyButton.Foreground = GetToggleBrush(_state.ToastEnabled);
        UpdateGlanceSurfaceStatus();
    }

    private void UpdateCountdownSelector()
    {
        if (_isSettingsVisible)
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
        AnimateMainContentNavigationOffset(isVisible);

        if (!_isMotionReady || !_animationsEnabled)
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
        _mainContentOffsetStoryboard?.Stop();
        _mainContentOffsetStoryboard = null;
        CountdownSelectorFrame.Visibility = Visibility.Collapsed;
        CountdownSelectorFrame.Opacity = 0;
        EnsureMainContentTranslateTransform().Y = 0;
        if (clearItems)
        {
            CountdownSelectorItems.Children.Clear();
        }
    }

    private void AnimateMainContentNavigationOffset(bool isSelectorVisible)
    {
        double targetOffset = isSelectorVisible ? MainContentNavigationOffset : 0;
        TranslateTransform transform = EnsureMainContentTranslateTransform();

        _mainContentOffsetStoryboard?.Stop();
        _mainContentOffsetStoryboard = null;

        if (!_isMotionReady || !_animationsEnabled)
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

    private void SetDeleteButtonVisible(bool isVisible)
    {
        bool wasVisible = DeleteButton.Visibility == Visibility.Visible;
        if (wasVisible == isVisible)
        {
            DeleteButton.Opacity = 1;
            DeleteButton.IsEnabled = isVisible;
            DeleteButton.IsHitTestVisible = isVisible;
            DeleteButton.IsTabStop = isVisible;
            return;
        }

        Dictionary<Button, double> previousCenters = CaptureToolbarButtonCenters();

        DeleteButton.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
        DeleteButton.Opacity = 1;
        DeleteButton.IsEnabled = isVisible;
        DeleteButton.IsHitTestVisible = isVisible;
        DeleteButton.IsTabStop = isVisible;
        RootGrid.UpdateLayout();
        AnimateToolbarButtonPositions(previousCenters);
    }

    private Dictionary<Button, double> CaptureToolbarButtonCenters()
    {
        Dictionary<Button, double> centers = new();
        if (!_isMotionReady || !_animationsEnabled)
        {
            return centers;
        }

        _toolbarPositionStoryboard?.Stop();
        _toolbarPositionStoryboard = null;
        foreach (Button button in GetToolbarButtons())
        {
            EnsureButtonTranslateTransform(button).X = 0;
        }

        RootGrid.UpdateLayout();
        foreach (Button button in GetToolbarButtons())
        {
            if (button.Visibility != Visibility.Visible || button.ActualWidth <= 0)
            {
                continue;
            }

            Point position = button.TransformToVisual(RootGrid).TransformPoint(new Point(0, 0));
            centers[button] = position.X + (button.ActualWidth / 2);
        }

        return centers;
    }

    private void AnimateToolbarButtonPositions(IReadOnlyDictionary<Button, double> previousCenters)
    {
        if (!_isMotionReady || !_animationsEnabled || previousCenters.Count == 0)
        {
            ResetToolbarButtonTransforms();
            return;
        }

        Storyboard storyboard = new();
        foreach (Button button in GetToolbarButtons())
        {
            if (button.Visibility != Visibility.Visible || !previousCenters.TryGetValue(button, out double previousCenter))
            {
                continue;
            }

            Point position = button.TransformToVisual(RootGrid).TransformPoint(new Point(0, 0));
            double currentCenter = position.X + (button.ActualWidth / 2);
            double offset = previousCenter - currentCenter;
            if (Math.Abs(offset) < 0.5)
            {
                continue;
            }

            TranslateTransform transform = EnsureButtonTranslateTransform(button);
            transform.X = offset;

            DoubleAnimation animation = new()
            {
                From = offset,
                To = 0,
                Duration = VisibilityMotionDuration,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                EnableDependentAnimation = true
            };
            Storyboard.SetTarget(animation, transform);
            Storyboard.SetTargetProperty(animation, nameof(TranslateTransform.X));
            storyboard.Children.Add(animation);
        }

        if (storyboard.Children.Count == 0)
        {
            ResetToolbarButtonTransforms();
            return;
        }

        _toolbarPositionStoryboard = storyboard;
        storyboard.Completed += (_, _) =>
        {
            if (ReferenceEquals(_toolbarPositionStoryboard, storyboard))
            {
                _toolbarPositionStoryboard = null;
                ResetToolbarButtonTransforms();
            }
        };
        storyboard.Begin();
    }

    private void ResetToolbarButtonTransforms()
    {
        foreach (Button button in GetToolbarButtons())
        {
            EnsureButtonTranslateTransform(button).X = 0;
        }
    }

    private IReadOnlyList<Button> GetToolbarButtons()
    {
        return new[] { NotifyButton, TileButton, DeleteButton, AddButton, MoreButton };
    }

    private static TranslateTransform EnsureButtonTranslateTransform(Button button)
    {
        if (button.RenderTransform is TranslateTransform transform)
        {
            return transform;
        }

        TranslateTransform newTransform = new();
        button.RenderTransform = newTransform;
        return newTransform;
    }

    private static Storyboard CreateOpacityStoryboard(UIElement element, double to)
    {
        DoubleAnimation animation = new()
        {
            To = to,
            Duration = VisibilityMotionDuration,
            EnableDependentAnimation = true
        };
        Storyboard.SetTarget(animation, element);
        Storyboard.SetTargetProperty(animation, nameof(UIElement.Opacity));

        Storyboard storyboard = new();
        storyboard.Children.Add(animation);
        return storyboard;
    }

    private Task AnimateCountdownSelectorItemRemovalAsync(string countdownId)
    {
        if (!_isMotionReady || !_animationsEnabled)
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
        GlanceSurfaceIcon.Glyph = _isWindows11OrGreater ? WidgetAddGlyph : TileGlyph;
    }

    private void UpdateGlanceSurfaceStatus()
    {
        if (!_isWindows11OrGreater)
        {
            TileButton.Foreground = GetToggleBrush(_state.TileEnabled);
            string tileTooltip = GetString(_state.TileEnabled ? "TileButton/DisableTooltip" : "TileButton/EnableTooltip");
            ToolTipService.SetToolTip(TileButton, tileTooltip);
            AutomationProperties.SetName(TileButton, tileTooltip);
            AutomationProperties.SetHelpText(TileButton, GetString("TileButton/HelpText"));
            return;
        }

        TileButton.Foreground = GetToggleBrush(true);
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

        if (_isWindows11OrGreater)
        {
            SyncWidgets();
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

    private async Task SaveCountdownAsync()
    {
        CountdownDraft draft = CreateDraftFromControls();
        UpdateCountdownPreview(draft);

        if (!draft.TryApplyTo(_state, DateTimeOffset.Now, out CountdownState? nextState) || nextState is null)
        {
            UpdateButtonStatus();
            return;
        }

        await ApplyCommittedStateAsync(nextState, reconcileStartupTask: false);
    }

    private async Task ApplyCommittedStateAsync(CountdownState state, bool reconcileStartupTask, bool refreshEditor = false)
    {
        _state = ApplyPreferencesToState(NormalizeStateForCurrentOs(state));
        SetDeleteButtonVisible(_state.CanRemoveCountdown);
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
        await _startupFeatureService.ReconcileAsync(_state, _preferences);
    }

    private async Task UpdateLiveTileFromStateAsync()
    {
        CountdownDisplayText displayText = CreateDisplayText();
        string daysLeft = displayText.FormatDaysLeft(_dateDifference, CultureInfo.CurrentCulture);
        if (_state.TileEnabled)
        {
            await _startMenuService.UpdateLiveTileAsync(daysLeft, displayText.FormatTitle(_state.Title));
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

    private void ShowStartupNotificationSavedToast(bool toastEnabled)
    {
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

        await ApplyCommittedStateAsync(_state.SelectCountdown(countdownId), reconcileStartupTask: false, refreshEditor: true);
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
            ShowWidgetHelpFlyout();
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

        CountdownDraft draft = CreateDraftFromControls(tileEnabled: true);
        if (!draft.TryApplyTo(_state, DateTimeOffset.Now, out CountdownState? nextState) || nextState is null)
        {
            UpdateCountdownPreview(draft);
            UpdateButtonStatus();
            return;
        }

        await TryApplyStartupBackedStateAsync(nextState);
    }

    private void ShowWidgetHelpFlyout()
    {
        _widgetHelpFlyout ??= CreateWidgetHelpFlyout();
        _widgetHelpFlyout.ShowAt(TileButton);
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
        bool toastEnabled = !_state.ToastEnabled;
        if (!toastEnabled)
        {
            await ApplyCommittedStateAsync(_state.With(toastEnabled: false), reconcileStartupTask: true);
            ShowStartupNotificationSavedToast(toastEnabled);
            return;
        }

        CountdownDraft draft = CreateDraftFromControls(toastEnabled: true);
        if (!draft.TryApplyTo(_state, DateTimeOffset.Now, out CountdownState? nextState) || nextState is null)
        {
            UpdateCountdownPreview(draft);
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
        CountdownItem newCountdown = new CountdownItem(
            Guid.NewGuid().ToString("N"),
            FormatNewCountdownTitle(_state.Countdowns.Count + 1),
            DateTimeOffset.Now.Date);

        await ApplyCommittedStateAsync(_state.AddCountdown(newCountdown, selectCountdown: true), reconcileStartupTask: false, refreshEditor: true);
    }

    private async void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_state.CanRemoveCountdown)
        {
            UpdateButtonStatus();
            return;
        }

        string removedCountdownId = _state.SelectedCountdownId;
        await AnimateCountdownSelectorItemRemovalAsync(removedCountdownId);
        await ApplyCommittedStateAsync(_state.RemoveCountdown(_state.SelectedCountdownId), reconcileStartupTask: true, refreshEditor: true);
    }

    private void SettingsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        ShowSettingsPage();
    }

    private void SettingsBackButton_Click(object sender, RoutedEventArgs e)
    {
        ShowCountdownPage();
    }

    private void ShowSettingsPage()
    {
        _widgetHelpFlyout?.Hide();
        _isSettingsVisible = true;
        HideCountdownSelectorImmediately(clearItems: true);
        MainContentScroller.Visibility = Visibility.Collapsed;
        SettingsContentScroller.Visibility = Visibility.Visible;
        SettingsContentScroller.ChangeView(null, 0, null, true);
        UpdateSettingsControls();
    }

    private void ShowCountdownPage()
    {
        _isSettingsVisible = false;
        SettingsContentScroller.Visibility = Visibility.Collapsed;
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
