using DateCountdown.Core;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace DateCountdown;

public sealed partial class MainWindow
{
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
        SetCountdownDisplayText(
            draft.TargetDate.HasValue
                ? FormatCountdownContent(draft.TargetDate.Value)
                : new CountdownDisplayContent(string.Empty, string.Empty, string.Empty),
            hasCountdownText: true);
        if (draft.TargetDate.HasValue)
        {
            _previewTargetDate = draft.TargetDate.Value;
        }

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
}
