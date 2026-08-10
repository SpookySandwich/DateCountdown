using DateCountdown.Core;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Foundation;

namespace DateCountdown;

public sealed partial class MainWindow
{
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
}
