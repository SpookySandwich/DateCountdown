using DateCountdown.Core;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DateCountdown;

public sealed partial class MainWindow
{
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
        return CountdownToolbarLayoutPolicy.IsActionVisible(
            action,
            _state,
            _preferences.ExperimentalFeaturesEnabled,
            _isDesktopOverlayAvailable);
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
        return CountdownToolbarLayoutPolicy.IsActionEnabled(
            action,
            _state,
            _preferences.ExperimentalFeaturesEnabled,
            _isDesktopOverlayAvailable,
            _isWindows11OrGreater,
            canCommitDraft);
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

    private IReadOnlyList<Button> GetToolbarButtons()
    {
        return new[] { NotifyButton, TileButton, DeleteButton, CancelEditorButton, SaveEditorButton, EditButton, AddButton, DesktopOverlayButton, MoreButton };
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
}
