namespace DateCountdown.Services;

internal static class OperatingSystemInfo
{
    public static bool IsWindows11OrGreater()
    {
        return System.OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000);
    }
}
