using DateCountdown.Core;
using DateCountdown.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace DateCountdown;

public sealed partial class MainWindow
{
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
        SavePreferences(_preferences.With(
            showDesktopCountdown: false,
            desktopOverlays: new[] { overlay }));
    }

    private void ReconcileDesktopOverlaySettingsWithState()
    {
        HashSet<string> countdownIds = new(_state.Countdowns.Select(countdown => countdown.Id), StringComparer.Ordinal);
        List<DesktopOverlaySettings> overlays = _preferences.DesktopOverlays
            .Where(overlay => countdownIds.Contains(overlay.CountdownId))
            .ToList();

        if (overlays.Count == _preferences.DesktopOverlays.Count)
        {
            return;
        }

        SavePreferences(_preferences.With(desktopOverlays: overlays));
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

    private void DesktopCountdownContentScroller_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        DesktopCountdownContentPanel.Width = Math.Min(640, Math.Max(360, e.NewSize.Width - 36));
    }

    private bool RefreshDesktopOverlayAvailability()
    {
        _isDesktopOverlayAvailable = DesktopWorkerWindowHostResolver.TryResolve(out _);
        return _isDesktopOverlayAvailable;
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

        return GetDesktopOverlay(_state.SelectedCountdownId);
    }

    private DesktopOverlaySettings GetDesktopOverlay(string countdownId)
    {
        return _preferences.DesktopOverlays.FirstOrDefault(overlay => string.Equals(overlay.CountdownId, countdownId, StringComparison.Ordinal)) ??
            new DesktopOverlaySettings(countdownId);
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
        SavePreferences(WithDesktopOverlay(overlay));
    }

    private CountdownPreferences WithDesktopOverlay(DesktopOverlaySettings overlay)
    {
        List<DesktopOverlaySettings> overlays = _preferences.DesktopOverlays
            .Where(existing => !string.Equals(existing.CountdownId, overlay.CountdownId, StringComparison.Ordinal))
            .ToList();
        if (overlay.ShouldPersist)
        {
            overlays.Add(overlay);
        }

        return _preferences.With(desktopOverlays: overlays);
    }

    private void DesktopCountdownButton_Click(object sender, RoutedEventArgs e)
    {
        OpenDesktopCountdownPage();
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

    private void DesktopCountdownBackButton_Click(object sender, RoutedEventArgs e)
    {
        ShowCountdownPage();
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
        string countdownId = selectedOverlay.CountdownId;
        if (!_desktopCountdownService.StartAdjusting(
            CreateDesktopCountdownContents(),
            countdownId,
            (x, y, scale) => SaveDesktopCountdownPlacement(countdownId, x, y, scale)))
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

        _preferences = WithDesktopOverlay(GetSelectedDesktopOverlay().With(css: DesktopCountdownCssTextBox.Text));
        _hasUnsavedOverlayCss = true;
        UpdateDesktopCountdown();
    }

    private void DesktopCountdownCssTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        CommitUnsavedOverlayCss();
    }

    private void CommitUnsavedOverlayCss()
    {
        if (!_hasUnsavedOverlayCss)
        {
            return;
        }

        _hasUnsavedOverlayCss = false;
        SavePreferences(_preferences);
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

    private void SaveDesktopCountdownPlacement(string countdownId, double x, double y, double scale)
    {
        SaveDesktopOverlay(GetDesktopOverlay(countdownId).With(positionX: x, positionY: y, scale: scale));
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
}
