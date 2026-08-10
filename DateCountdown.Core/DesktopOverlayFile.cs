using System;
using System.Collections.Generic;
using System.IO;

namespace DateCountdown.Core;

public static class DesktopOverlayFile
{
    public const string FileName = "desktop-overlays.json";

    public static IReadOnlyList<DesktopOverlaySettings> Load(string filePath, string? legacyValue)
    {
        if (!File.Exists(filePath))
        {
            return DesktopOverlaySettingsJson.Deserialize(legacyValue);
        }

        try
        {
            return DesktopOverlaySettingsJson.Deserialize(File.ReadAllText(filePath));
        }
        catch (IOException)
        {
            return Array.Empty<DesktopOverlaySettings>();
        }
    }

    public static void Save(string filePath, IEnumerable<DesktopOverlaySettings?> overlays)
    {
        File.WriteAllText(filePath, DesktopOverlaySettingsJson.Serialize(overlays));
    }
}
