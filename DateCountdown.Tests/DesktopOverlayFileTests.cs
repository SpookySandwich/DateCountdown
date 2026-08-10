using DateCountdown.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace DateCountdown.Tests;

public sealed class DesktopOverlayFileTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory().FullName;

    private string FilePath => Path.Combine(_directory, DesktopOverlayFile.FileName);

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public void SaveAndLoad_RoundTripsOverlaysLargerThanLegacySettingsLimit()
    {
        List<DesktopOverlaySettings> overlays = Enumerable.Range(0, 3)
            .Select(index => new DesktopOverlaySettings(
                $"countdown{index}",
                isEnabled: true,
                positionX: 0.25,
                positionY: 0.75,
                scale: 1.5,
                css: new string('a', CountdownPreferences.MaxDesktopCountdownCssLength)))
            .ToList();

        DesktopOverlayFile.Save(FilePath, overlays);
        IReadOnlyList<DesktopOverlaySettings> loaded = DesktopOverlayFile.Load(FilePath, legacyValue: null);

        Assert.True(new FileInfo(FilePath).Length > 8192);
        Assert.Equal(overlays.Select(overlay => overlay.CountdownId), loaded.Select(overlay => overlay.CountdownId));
        Assert.All(loaded, overlay =>
        {
            Assert.True(overlay.IsEnabled);
            Assert.Equal(0.25, overlay.PositionX);
            Assert.Equal(0.75, overlay.PositionY);
            Assert.Equal(1.5, overlay.Scale);
            Assert.Equal(CountdownPreferences.MaxDesktopCountdownCssLength, overlay.Css.Length);
        });
    }

    [Fact]
    public void Load_WithoutFile_ReadsLegacyValue()
    {
        string legacyValue = DesktopOverlaySettingsJson.Serialize(
            new[] { new DesktopOverlaySettings("legacy", isEnabled: true) });

        IReadOnlyList<DesktopOverlaySettings> loaded = DesktopOverlayFile.Load(FilePath, legacyValue);

        DesktopOverlaySettings overlay = Assert.Single(loaded);
        Assert.Equal("legacy", overlay.CountdownId);
        Assert.True(overlay.IsEnabled);
    }

    [Fact]
    public void Load_WithFile_IgnoresLegacyValue()
    {
        DesktopOverlayFile.Save(FilePath, new[] { new DesktopOverlaySettings("current", isEnabled: true) });
        string legacyValue = DesktopOverlaySettingsJson.Serialize(
            new[] { new DesktopOverlaySettings("legacy", isEnabled: true) });

        IReadOnlyList<DesktopOverlaySettings> loaded = DesktopOverlayFile.Load(FilePath, legacyValue);

        Assert.Equal("current", Assert.Single(loaded).CountdownId);
    }

    [Fact]
    public void Load_WithoutFileOrLegacyValue_ReturnsEmpty()
    {
        Assert.Empty(DesktopOverlayFile.Load(FilePath, legacyValue: null));
    }
}
