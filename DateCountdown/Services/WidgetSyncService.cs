using DateCountdown.Core;
using Microsoft.Windows.Widgets;
using Microsoft.Windows.Widgets.Providers;
using System;

namespace DateCountdown.Services;

internal sealed class WidgetSyncService
{
    private readonly bool _isEnabled;

    public WidgetSyncService(bool isEnabled)
    {
        _isEnabled = isEnabled;
    }

    public void UpdatePinnedWidgets(CountdownState state, DateTimeOffset now, CountdownDisplayText displayText)
    {
        if (!_isEnabled)
        {
            return;
        }

        try
        {
            WidgetManager widgetManager = WidgetManager.GetDefault();
            var widgetInfos = widgetManager.GetWidgetInfos();
            if (widgetInfos != null)
            {
                foreach (WidgetInfo widgetInfo in widgetInfos)
                {
                    if (!widgetInfo.WidgetContext.IsActive)
                    {
                        continue;
                    }

                    CountdownWidgetSize size = ToCountdownWidgetSize(widgetInfo.WidgetContext.Size);
                    WidgetUpdateRequestOptions options = new(widgetInfo.WidgetContext.Id)
                    {
                        Template = CountdownWidgetContent.BuildTemplateJson(state, size),
                        Data = CountdownWidgetContent.BuildDataJson(state, now, displayText, size),
                        CustomState = state.SelectedCountdownId
                    };

                    widgetManager.UpdateWidget(options);
                }
            }
        }
        catch
        {
        }
    }

    private static CountdownWidgetSize ToCountdownWidgetSize(WidgetSize size)
    {
        return size switch
        {
            WidgetSize.Small => CountdownWidgetSize.Small,
            WidgetSize.Medium => CountdownWidgetSize.Medium,
            _ => CountdownWidgetSize.Large
        };
    }
}
