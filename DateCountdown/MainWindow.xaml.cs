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
using Microsoft.Windows.ApplicationModel.Resources;
using System;
using System.Globalization;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.System;

namespace DateCountdown;

public sealed partial class MainWindow : Window
{
    private const string TileGlyph = "\uECA5";
    private const string WidgetAddGlyph = "\uF036";

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
    private double _countdownSelectorDragStartOffset;
    private double _countdownSelectorDragStartX;
    private bool _didDragCountdownSelector;
    private bool _isDraggingCountdownSelector;
    private bool _isUpdatingControls;
    private DateTimeOffset _lastCountdownSelectorDragAt = DateTimeOffset.MinValue;
    private Flyout? _widgetHelpFlyout;

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

        foreach (CountdownItem countdown in _state.Countdowns)
        {
            if (countdown.ToastEnabled)
            {
                string daysLeft = FormatDaysLeft(CountdownLogic.CalculateDaysLeft(countdown.TargetDate, DateTimeOffset.Now));
                _startupNotificationService.ShowToast(daysLeft, countdown.Title);
            }
        }

        if (_state.TileEnabled && !_isWindows11OrGreater)
        {
            await _startMenuService.UpdateLiveTileAsync(FormatDaysLeft(_dateDifference), _state.Title);
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
            UpdateEditorFromState();
        }
        finally
        {
            _isUpdatingControls = false;
        }

        string toastTooltip = GetString("ToastButton/Tooltip");
        string addTooltip = GetString("AddCountdownButton/Tooltip");
        string deleteTooltip = GetString("DeleteCountdownButton/Tooltip");

        ConfigureGlanceSurfaceButton();
        ToolTipService.SetToolTip(NotifyButton, toastTooltip);
        ToolTipService.SetToolTip(AddButton, addTooltip);
        ToolTipService.SetToolTip(DeleteButton, deleteTooltip);
        AutomationProperties.SetName(NotifyButton, toastTooltip);
        AutomationProperties.SetName(AddButton, addTooltip);
        AutomationProperties.SetName(DeleteButton, deleteTooltip);
        AutomationProperties.SetHelpText(NotifyButton, GetString("ToastButton/HelpText"));
        AutomationProperties.SetHelpText(AddButton, GetString("AddCountdownButton/HelpText"));
        AutomationProperties.SetHelpText(DeleteButton, GetString("DeleteCountdownButton/HelpText"));
        AutomationProperties.SetName(CountdownSelectorRail, GetString("CountdownSelector/Name"));
        AutomationProperties.SetName(DatePickerTargetDate, GetString("TargetDatePicker/Name"));
        AutomationProperties.SetName(TextBoxTitle, GetString("Title/PlaceholderText"));

        UpdateButtonStatus();
    }

    private void UpdateEditorFromState()
    {
        UpdateCountdownPreview();
        UpdateCountdownSelector();
        TextBoxTitle.PlaceholderText = GetString("Title/PlaceholderText");
        TextBoxTitle.Text = _state.Title;
        DatePickerTargetDate.MinDate = DateTimeOffset.Now.Date;
        DatePickerTargetDate.Date = _state.TargetDate;
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
        AddButton.IsEnabled = true;
        DeleteButton.IsEnabled = _state.CanRemoveCountdown;
        NotifyButton.Foreground = GetToggleBrush(_state.ToastEnabled);
        UpdateGlanceSurfaceStatus();
    }

    private void UpdateCountdownSelector()
    {
        bool wasUpdatingControls = _isUpdatingControls;
        _isUpdatingControls = true;
        try
        {
            CountdownSelectorFrame.Visibility = _state.Countdowns.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
            CountdownSelectorItems.Children.Clear();
            UpdateCountdownSelectorContentWidth();

            if (_state.Countdowns.Count <= 1)
            {
                return;
            }

            for (int i = 0; i < _state.Countdowns.Count; i++)
            {
                CountdownItem countdown = _state.Countdowns[i];
                bool isSelected = string.Equals(countdown.Id, _state.SelectedCountdownId, StringComparison.Ordinal);
                FrameworkElement item = CreateCountdownSelectorItem(countdown, i, isSelected);

                CountdownSelectorItems.Children.Add(item);
                if (isSelected)
                {
                    BringSelectedCountdownIntoView();
                }
            }
        }
        finally
        {
            _isUpdatingControls = wasUpdatingControls;
        }
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
            ToolTipService.SetToolTip(TileButton, GetString("TileButton/Tooltip"));
            AutomationProperties.SetName(TileButton, GetString("TileButton/Tooltip"));
            AutomationProperties.SetHelpText(TileButton, GetString("TileButton/HelpText"));
            return;
        }

        TileButton.Foreground = GetToggleBrush(true);
        ToolTipService.SetToolTip(TileButton, GetString("WidgetButton/Tooltip"));
        AutomationProperties.SetName(TileButton, GetString("WidgetButton/Tooltip"));
        AutomationProperties.SetHelpText(TileButton, GetString("WidgetButton/HelpText"));
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

        if (!draft.TryApplyTo(_state, DateTimeOffset.Now, out CountdownState? nextState) || nextState is null)
        {
            UpdateButtonStatus();
            return;
        }

        await ApplyCommittedStateAsync(nextState, reconcileStartupTask: false);
    }

    private async Task ApplyCommittedStateAsync(CountdownState state, bool reconcileStartupTask, bool refreshEditor = false)
    {
        _state = NormalizeStateForCurrentOs(state);
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

    private void ShowStartupNotificationSavedToast(bool toastEnabled)
    {
        string countdownTitle = string.IsNullOrWhiteSpace(_state.Title)
            ? GetString("AppName")
            : _state.Title;
        string contentFormat = GetString(toastEnabled
            ? "ToastButton/EnabledSavedContentFormat"
            : "ToastButton/DisabledSavedContentFormat");

        _startupNotificationService.ShowToast(
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
        if (!draft.TryApplyTo(_state, DateTimeOffset.Now, out CountdownState? nextState) || nextState is null)
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

        if (!await _startupFeatureService.EnsureAvailableForAsync(nextState))
        {
            _startupNotificationService.ShowToast(
                GetString("CreateStartupTaskFailedNotification/Title"),
                GetString("CreateStartupTaskFailedNotification/Content"));
            UpdateButtonStatus();
            return;
        }

        await ApplyCommittedStateAsync(nextState, reconcileStartupTask: false);
        ShowStartupNotificationSavedToast(toastEnabled);
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

        await ApplyCommittedStateAsync(_state.RemoveCountdown(_state.SelectedCountdownId), reconcileStartupTask: true, refreshEditor: true);
    }

}
