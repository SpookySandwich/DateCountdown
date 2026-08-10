using DateCountdown.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Globalization;
using System.Threading.Tasks;
using Windows.System;

namespace DateCountdown;

public sealed partial class MainWindow
{
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
}
