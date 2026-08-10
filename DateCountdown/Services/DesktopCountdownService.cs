using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace DateCountdown.Services;

internal sealed class DesktopCountdownService : IDisposable
{
    private readonly DesktopCountdownWindow _window = new();
    private readonly DesktopCountdownAdjustmentWindow _adjustmentWindow = new();
    private IReadOnlyList<DesktopCountdownContent> _contents = Array.Empty<DesktopCountdownContent>();
    private string _adjustingCountdownId = string.Empty;
    private Action<double, double, double>? _placementChanged;
    private Action<double, double, double>? _placementCommitted;
    private bool _isAdjusting;

    public bool IsVisible => _window.IsVisible;

    public bool IsAdjusting => _isAdjusting;

    public bool ShowOrUpdate(IReadOnlyList<DesktopCountdownContent> contents)
    {
        _contents = new ReadOnlyCollection<DesktopCountdownContent>(contents.ToList());
        if (_contents.Count == 0)
        {
            Hide();
            return true;
        }

        if (_isAdjusting)
        {
            _ = _window.ShowOrUpdate(_contents);
            DesktopCountdownContent? adjustingContent = FindContent(_adjustingCountdownId);
            if (adjustingContent is null)
            {
                StopAdjusting();
                return true;
            }

            return ShowAdjustment(adjustingContent);
        }

        return _window.ShowOrUpdate(_contents);
    }

    public bool StartAdjusting(IReadOnlyList<DesktopCountdownContent> contents, string countdownId, Action<double, double, double> placementCommitted)
    {
        _contents = new ReadOnlyCollection<DesktopCountdownContent>(contents.ToList());
        DesktopCountdownContent? selectedContent = FindContent(countdownId);
        if (selectedContent is null)
        {
            return false;
        }

        _adjustingCountdownId = countdownId;
        _placementChanged = (x, y, scale) =>
        {
            _contents = new ReadOnlyCollection<DesktopCountdownContent>(_contents
                .Select(content => string.Equals(content.CountdownId, countdownId, StringComparison.Ordinal)
                    ? content with
                    {
                        PositionX = x,
                        PositionY = y,
                        Scale = scale
                    }
                    : content)
                .ToList());

            _ = _window.ShowOrUpdate(_contents);
        };
        _placementCommitted = placementCommitted;
        _isAdjusting = true;
        _ = _window.ShowOrUpdate(_contents);
        return ShowAdjustment(selectedContent);
    }

    public void StopAdjusting()
    {
        _isAdjusting = false;
        _adjustingCountdownId = string.Empty;
        _placementChanged = null;
        _placementCommitted = null;
        _adjustmentWindow.Hide();
    }

    public bool Refresh()
    {
        return _contents.Count > 0 && ShowOrUpdate(_contents);
    }

    public void Hide()
    {
        _isAdjusting = false;
        _adjustingCountdownId = string.Empty;
        _placementChanged = null;
        _placementCommitted = null;
        _contents = Array.Empty<DesktopCountdownContent>();
        _window.Hide();
        _adjustmentWindow.Hide();
    }

    public void Dispose()
    {
        _window.Dispose();
        _adjustmentWindow.Dispose();
    }

    private DesktopCountdownContent? FindContent(string countdownId)
    {
        return _contents.FirstOrDefault(content => string.Equals(content.CountdownId, countdownId, StringComparison.Ordinal));
    }

    private bool ShowAdjustment(DesktopCountdownContent content)
    {
        bool shown = _adjustmentWindow.ShowOrUpdate(content, _placementChanged!, _placementCommitted!);
        if (!shown)
        {
            StopAdjusting();
        }

        return shown;
    }
}
