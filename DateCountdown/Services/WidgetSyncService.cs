using DateCountdown.Core;
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
            foreach (string widgetId in widgetManager.GetWidgetIds())
            {
                WidgetUpdateRequestOptions options = new(widgetId)
                {
                    Template = CountdownWidgetContent.BuildTemplateJson(),
                    Data = CountdownWidgetContent.BuildDataJson(state.Title, state.TargetDate, now, displayText),
                    CustomState = state.TargetDate.ToString("O")
                };

                widgetManager.UpdateWidget(options);
            }
        }
        catch
        {
        }
    }
}
