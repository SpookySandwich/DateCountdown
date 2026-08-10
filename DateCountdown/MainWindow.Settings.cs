using DateCountdown.Core;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Windows.ApplicationModel;

namespace DateCountdown;

public sealed partial class MainWindow
{
    private void InitializeHourDisplayModeOptions()
    {
        _hourDisplayModeOptions =
        [
            new HourDisplayModeOption(CountdownHourDisplayMode.DaysOnly, GetString("HourDisplayMode/DaysOnly")),
            new HourDisplayModeOption(CountdownHourDisplayMode.NearDeadline, GetString("HourDisplayMode/NearDeadline")),
            new HourDisplayModeOption(CountdownHourDisplayMode.HoursOnly, GetString("HourDisplayMode/HoursOnly")),
            new HourDisplayModeOption(CountdownHourDisplayMode.DaysAndHours, GetString("HourDisplayMode/DaysAndHours"))
        ];

        HourDisplayModeComboBox.ItemsSource = _hourDisplayModeOptions;
        HourDisplayModeComboBox.DisplayMemberPath = nameof(HourDisplayModeOption.DisplayName);
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

    private void UpdateSettingsPreview()
    {
        if (SettingsPreviewDaysText is null ||
            SettingsPreviewHoursText is null ||
            SettingsPreviewTitleText is null)
        {
            return;
        }

        bool hasCountdown = _state.HasCountdowns;
        string emptyStateText = GetString("EmptyState/Title");
        CountdownDisplayContent content = hasCountdown
            ? FormatCountdownContent(_previewTargetDate)
            : new CountdownDisplayContent(emptyStateText, string.Empty, emptyStateText);
        SettingsPreviewDaysText.Text = content.PrimaryText;
        SettingsPreviewHoursText.Text = content.SecondaryText;
        SettingsPreviewHoursText.Visibility = content.HasSecondaryText ? Visibility.Visible : Visibility.Collapsed;
        double durationTextSize = hasCountdown
            ? CountdownMainContentLayout.CalculatePrimaryDurationTextSize(_preferences.DaysTextSize, content.LayoutKind)
            : _preferences.DaysTextSize;
        SettingsPreviewDaysText.FontSize = durationTextSize;
        SettingsPreviewHoursText.FontSize = CountdownMainContentLayout.CalculateSecondaryDurationTextSize(
            _preferences.DaysTextSize,
            content.LayoutKind);
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

    private void SettingsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        ShowSettingsPage();
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

        SavePreferences(_preferences.With(sortCountdownsByDaysLeft: SortCountdownsToggle.IsOn));
        _state = ApplyPreferencesToState(_state);
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

        SavePreferences(_preferences.With(autoDeleteExpiredCountdowns: AutoDeleteExpiredCountdownsToggle.IsOn));
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

        SavePreferences(_preferences.With(toolbarLayout: new CountdownToolbarLayout(actions, primaryActionCount)));
        UpdateSettingsControls();
        UpdateButtonStatus();
    }

    private void DaysTextSizeSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_isUpdatingSettingsControls)
        {
            return;
        }

        SavePreferences(_preferences.With(daysTextSize: e.NewValue));
        ApplyDisplaySizePreference();
    }

    private void TitleTextSizeSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_isUpdatingSettingsControls)
        {
            return;
        }

        SavePreferences(_preferences.With(titleTextSize: e.NewValue));
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

        SavePreferences(_preferences.With(hourDisplayMode: option.Mode));
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
        SavePreferences(_preferences.With(hoursDisplayThresholdDays: thresholdDays));
        RefreshDateDependentSurfaces();
        UpdateSettingsControls();
    }

    private void ReduceMotionToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingSettingsControls)
        {
            return;
        }

        SavePreferences(_preferences.With(reduceMotion: ReduceMotionToggle.IsOn));
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

        SavePreferences(nextPreferences);
        await ReconcileStartupTaskAsync();
        UpdateSettingsControls();
    }

    private void ExperimentalFeaturesToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingSettingsControls)
        {
            return;
        }

        SavePreferences(_preferences.With(experimentalFeaturesEnabled: ExperimentalFeaturesToggle.IsOn));
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
}
