using System;
using TuneIsland.Core;
using Xunit;

namespace TuneIsland.Tests;

public class TimeFormatterTests
{
    [Theory]
    [InlineData(0, "0:00")]
    [InlineData(5, "0:05")]
    [InlineData(65, "1:05")]
    [InlineData(3599, "59:59")]
    [InlineData(3600, "1:00:00")]
    [InlineData(3725, "1:02:05")]
    public void Format_UsesMinutesOrHours(int totalSeconds, string expected)
    {
        Assert.Equal(expected, TimeFormatter.Format(TimeSpan.FromSeconds(totalSeconds)));
    }

    [Fact]
    public void Format_NegativeIsZero()
    {
        Assert.Equal("0:00", TimeFormatter.Format(TimeSpan.FromSeconds(-10)));
    }
}

public class DominantColorExtractorTests
{
    [Fact]
    public void Extract_PicksDominantSaturatedColor()
    {
        // 90% vermelho saturado e 10% cinza: a cor escolhida deve ser avermelhada.
        var pixels = new byte[10 * 4];
        for (var i = 0; i < 10; i++)
        {
            var offset = i * 4;
            if (i < 9)
            {
                pixels[offset] = 0;       // B
                pixels[offset + 1] = 0;   // G
                pixels[offset + 2] = 220; // R
                pixels[offset + 3] = 255; // A
            }
            else
            {
                pixels[offset] = 128;
                pixels[offset + 1] = 128;
                pixels[offset + 2] = 128;
                pixels[offset + 3] = 255;
            }
        }

        var color = DominantColorExtractor.Extract(pixels);

        Assert.True(color.R > 150);
        Assert.True(color.G < 60);
        Assert.True(color.B < 60);
    }

    [Fact]
    public void Extract_GrayscaleFallsBackToDefault()
    {
        var pixels = new byte[] { 100, 100, 100, 255, 200, 200, 200, 255 };

        Assert.Equal(DominantColorExtractor.Fallback, DominantColorExtractor.Extract(pixels));
    }

    [Fact]
    public void Extract_IgnoresTransparentPixels()
    {
        // Único pixel colorido é transparente; sobra só cinza -> fallback.
        var pixels = new byte[] { 0, 0, 255, 0, 128, 128, 128, 255 };

        Assert.Equal(DominantColorExtractor.Fallback, DominantColorExtractor.Extract(pixels));
    }

    [Fact]
    public void EnsureVisibleOnDark_BrightensDarkColorsKeepingHue()
    {
        var result = DominantColorExtractor.EnsureVisibleOnDark(new RgbColor(40, 20, 10));

        Assert.Equal(150, Math.Max(result.R, Math.Max(result.G, result.B)));
        Assert.True(result.R > result.G && result.G > result.B);
    }

    [Fact]
    public void EnsureVisibleOnDark_LeavesBrightColorsUnchanged()
    {
        var color = new RgbColor(200, 100, 50);

        Assert.Equal(color, DominantColorExtractor.EnsureVisibleOnDark(color));
    }
}

public class SettingsStoreTests
{
    [Fact]
    public void RoundTrip_PreservesValues()
    {
        var original = new AppSettings
        {
            Style = IslandStyle.Floating,
            MonitorIndex = 1,
            Width = 500,
            AutoExpandSeconds = 5,
            StartWithWindows = true,
            HideWhenIdle = false,
            HideInFullscreen = false
        };

        var restored = SettingsStore.Deserialize(SettingsStore.Serialize(original));

        Assert.Equal(IslandStyle.Floating, restored.Style);
        Assert.Equal(1, restored.MonitorIndex);
        Assert.Equal(500, restored.Width);
        Assert.Equal(5, restored.AutoExpandSeconds);
        Assert.True(restored.StartWithWindows);
        Assert.False(restored.HideWhenIdle);
        Assert.False(restored.HideInFullscreen);
    }

    [Fact]
    public void Serialize_WritesStyleAsString()
    {
        var json = SettingsStore.Serialize(new AppSettings { Style = IslandStyle.Floating });

        Assert.Contains("\"Floating\"", json);
    }

    [Fact]
    public void Deserialize_ClampsOutOfRangeValues()
    {
        var settings = SettingsStore.Deserialize("{\"Width\": 5000, \"AutoExpandSeconds\": 0, \"MonitorIndex\": -3}");

        Assert.Equal(AppSettings.MaxWidth, settings.Width);
        Assert.Equal(AppSettings.MinAutoExpandSeconds, settings.AutoExpandSeconds);
        Assert.Equal(0, settings.MonitorIndex);
    }

    [Fact]
    public void Deserialize_MissingFieldsUseDefaults()
    {
        var settings = SettingsStore.Deserialize("{}");

        Assert.Equal(IslandStyle.Attached, settings.Style);
        Assert.Equal(265, settings.Width);
        Assert.False(settings.HideWhenIdle);
    }
}

public class MediaSnapshotTests
{
    private static MediaSnapshot Make(bool playing, TimeSpan position, TimeSpan duration, DateTimeOffset captured) =>
        new("app", "title", "artist", playing, position, duration, captured, null, "key");

    [Fact]
    public void EstimatePosition_AdvancesWhilePlaying()
    {
        var captured = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var snapshot = Make(true, TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(3), captured);

        var estimate = snapshot.EstimatePosition(captured.AddSeconds(5));

        Assert.Equal(TimeSpan.FromSeconds(15), estimate);
    }

    [Fact]
    public void EstimatePosition_DoesNotAdvanceWhenPaused()
    {
        var captured = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var snapshot = Make(false, TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(3), captured);

        Assert.Equal(TimeSpan.FromSeconds(10), snapshot.EstimatePosition(captured.AddSeconds(5)));
    }

    [Fact]
    public void EstimatePosition_ClampsToDuration()
    {
        var captured = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var snapshot = Make(true, TimeSpan.FromSeconds(58), TimeSpan.FromMinutes(1), captured);

        Assert.Equal(TimeSpan.FromMinutes(1), snapshot.EstimatePosition(captured.AddSeconds(30)));
    }
}
