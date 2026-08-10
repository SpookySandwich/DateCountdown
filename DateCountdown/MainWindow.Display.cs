using DateCountdown.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System;

namespace DateCountdown;

public sealed partial class MainWindow
{
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

    private void ApplyDisplaySizePreference()
    {
        ApplyCountdownTextStyle(_state.HasCountdowns || _isEditorVisible);
        MainContentPanel.MaxWidth = CountdownMainContentLayout.CalculateMainContentMaxWidth(
            _preferences.DaysTextSize,
            _durationLayoutKind);
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

    private void UpdateCountdownPreview()
    {
        if (_state.SelectedCountdown is not CountdownItem selectedCountdown)
        {
            _previewTargetDate = CreateDefaultTargetDate();
            string emptyStateText = GetString("EmptyState/Title");
            SetCountdownDisplayText(
                new CountdownDisplayContent(emptyStateText, string.Empty, emptyStateText),
                hasCountdownText: false);
            TextBlockTitle.Text = string.Empty;
            AutomationProperties.SetName(CountdownDisplayPanel, emptyStateText);
            SetCountdownSurfaceVisible();
            UpdateSettingsPreview();
            UpdateDesktopCountdown();
            return;
        }

        _previewTargetDate = selectedCountdown.TargetDate;
        TextBlockTitle.Text = selectedCountdown.Title;
        CountdownDisplayContent content = FormatCountdownContent(_previewTargetDate);
        SetCountdownDisplayText(content, hasCountdownText: true);
        AutomationProperties.SetName(CountdownDisplayPanel, $"{selectedCountdown.Title}, {content.AccessibleText}");
        SetCountdownSurfaceVisible();
        UpdateSettingsPreview();
        UpdateDesktopCountdown();
    }

    private void SetCountdownDisplayText(CountdownDisplayContent content, bool hasCountdownText)
    {
        TextBlockDays.Text = content.PrimaryText;
        TextBlockHours.Text = content.SecondaryText;
        TextBlockHours.Visibility = content.HasSecondaryText ? Visibility.Visible : Visibility.Collapsed;
        _durationLayoutKind = hasCountdownText ? content.LayoutKind : CountdownDisplayLayoutKind.SingleLine;
        AutomationProperties.SetName(CountdownDurationPanel, content.AccessibleText);
        ApplyCountdownTextStyle(hasCountdownText);
    }

    private void ApplyCountdownTextStyle(bool hasCountdownText)
    {
        double durationTextSize = hasCountdownText
            ? CountdownMainContentLayout.CalculatePrimaryDurationTextSize(_preferences.DaysTextSize, _durationLayoutKind)
            : 32;
        TextBlockDays.FontSize = durationTextSize;
        TextBlockHours.FontSize = CountdownMainContentLayout.CalculateSecondaryDurationTextSize(
            _preferences.DaysTextSize,
            _durationLayoutKind);
        TextBlockDays.MaxLines = hasCountdownText ? 1 : 3;
        TextBlockDays.TextTrimming = hasCountdownText ? TextTrimming.CharacterEllipsis : TextTrimming.None;
        TextBlockDays.TextWrapping = hasCountdownText ? TextWrapping.NoWrap : TextWrapping.WrapWholeWords;
        TextBlockDays.Opacity = hasCountdownText ? 1 : 0.72;
        TextBlockTitle.FontSize = _preferences.TitleTextSize;
        TextBlockTitle.Visibility = hasCountdownText ? Visibility.Visible : Visibility.Collapsed;
        MainContentPanel.MaxWidth = CountdownMainContentLayout.CalculateMainContentMaxWidth(
            _preferences.DaysTextSize,
            _durationLayoutKind);
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

    private Brush GetResourceBrush(string resourceKey, Brush fallback)
    {
        if (Application.Current.Resources.TryGetValue(resourceKey, out object resource) && resource is Brush brush)
        {
            return brush;
        }

        return fallback;
    }

    private void MainContentScroller_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateMainContentPanelPadding();
    }
}
