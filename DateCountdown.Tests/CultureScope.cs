using System;
using System.Globalization;
using System.Threading;

namespace DateCountdown.Tests;

internal sealed class CultureScope : IDisposable
{
    private readonly CultureInfo _currentCulture;
    private readonly CultureInfo _currentUICulture;

    public CultureScope(string cultureName)
    {
        _currentCulture = CultureInfo.CurrentCulture;
        _currentUICulture = CultureInfo.CurrentUICulture;

        CultureInfo culture = CultureInfo.GetCultureInfo(cultureName);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
    }

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _currentCulture;
        CultureInfo.CurrentUICulture = _currentUICulture;
        Thread.CurrentThread.CurrentCulture = _currentCulture;
        Thread.CurrentThread.CurrentUICulture = _currentUICulture;
    }
}
