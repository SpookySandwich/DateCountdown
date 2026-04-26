using DateCountdown.Core;
using Microsoft.Windows.ApplicationModel.Resources;
using Microsoft.Windows.Widgets.Providers;
using System;
using System.Collections.Generic;
using System.Threading;
using Windows.Storage;

namespace DateCountdown.WidgetProvider;

internal sealed class CountdownWidgetProvider : IWidgetProvider, IDisposable
{
    private readonly ManualResetEventSlim _exitEvent = new(false);
    private readonly HashSet<string> _runningWidgetIds = new(StringComparer.Ordinal);

    public void Activate(WidgetContext widgetContext)
    {
        AddWidget(widgetContext);
        UpdateWidget(widgetContext.Id);
    }

    public void CreateWidget(WidgetContext widgetContext)
    {
        AddWidget(widgetContext);
        UpdateWidget(widgetContext.Id);
    }

    public void Deactivate(string widgetId)
    {
        RemoveWidget(widgetId);
    }

    public void DeleteWidget(string widgetId, string customState)
    {
        RemoveWidget(widgetId);
    }

    public void OnActionInvoked(WidgetActionInvokedArgs actionInvokedArgs)
    {
        if (string.Equals(actionInvokedArgs.Verb, "refresh", StringComparison.OrdinalIgnoreCase))
        {
            UpdateWidget(actionInvokedArgs.WidgetContext.Id);
        }
    }

    public void OnWidgetContextChanged(WidgetContextChangedArgs contextChangedArgs)
    {
        AddWidget(contextChangedArgs.WidgetContext);
        UpdateWidget(contextChangedArgs.WidgetContext.Id);
    }

    public void WaitForExit()
    {
        _exitEvent.Wait();
    }

    public void Dispose()
    {
        _exitEvent.Dispose();
    }

    private void AddWidget(WidgetContext widgetContext)
    {
        if (!string.Equals(widgetContext.DefinitionId, CountdownWidgetContent.DefinitionId, StringComparison.Ordinal))
        {
            return;
        }

        _runningWidgetIds.Add(widgetContext.Id);
        _exitEvent.Reset();
    }

    private void RemoveWidget(string widgetId)
    {
        _runningWidgetIds.Remove(widgetId);
        if (_runningWidgetIds.Count == 0)
        {
            _exitEvent.Set();
        }
    }

    private static void UpdateWidget(string widgetId)
    {
        try
        {
            CountdownState state = ReadState();
            WidgetUpdateRequestOptions options = new(widgetId)
            {
                Template = CountdownWidgetContent.BuildTemplateJson(),
                Data = CountdownWidgetContent.BuildDataJson(state.Title, state.TargetDate, DateTimeOffset.Now, CreateDisplayText()),
                CustomState = state.TargetDate.ToString("O")
            };

            WidgetManager.GetDefault().UpdateWidget(options);
        }
        catch
        {
        }
    }

    private static CountdownState ReadState()
    {
        ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
        string title = localSettings.Values[CountdownSettingsKeys.Title] as string ?? string.Empty;
        DateTimeOffset targetDate = CountdownLogic.ReadDateValue(localSettings.Values[CountdownSettingsKeys.TargetDate], DateTimeOffset.Now);

        return new CountdownState(title, targetDate, tileEnabled: false, toastEnabled: false);
    }

    private static CountdownDisplayText CreateDisplayText()
    {
        return new CountdownDisplayText(
            GetString("AppName"),
            GetString("DaysLeftOneText"),
            GetString("DaysLeftManyText"));
    }

    private static string GetString(string key)
    {
        try
        {
            return new ResourceLoader().GetString(key);
        }
        catch
        {
            return string.Empty;
        }
    }
}
