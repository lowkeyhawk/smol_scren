using System;
using System.IO;
using Xunit;
using smolscren;

public class SettingsTests : IDisposable
{
    private readonly string _testDir;

    public SettingsTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "smolscren_test_" + Guid.NewGuid());
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    [Fact]
    public void Load_ReturnsDefaults_WhenNoFileExists()
    {
        var settings = Settings.Load(_testDir);
        Assert.Equal("", settings.VideoPath);
        Assert.Equal(100, settings.Left);
        Assert.Equal(100, settings.Top);
        Assert.Equal(400, settings.Width);
        Assert.Equal(300, settings.Height);
        Assert.True(settings.KeepOnTop);
    }

    [Fact]
    public void Save_WritesJsonFile()
    {
        var settings = new Settings { VideoPath = @"C:\test\video.mp4", Left = 50 };
        settings.Save(_testDir);
        var path = Path.Combine(_testDir, "settings.json");
        Assert.True(File.Exists(path));
        var json = File.ReadAllText(path);
        Assert.Contains("video.mp4", json);
    }

    [Fact]
    public void Load_RoundTrips_SavedValues()
    {
        var settings = new Settings { VideoPath = @"C:\test\test.mp4", Left = 200, Top = 150, Width = 500, Height = 350, KeepOnTop = false };
        settings.Save(_testDir);
        var loaded = Settings.Load(_testDir);
        Assert.Equal(settings.VideoPath, loaded.VideoPath);
        Assert.Equal(settings.Left, loaded.Left);
        Assert.Equal(settings.Top, loaded.Top);
        Assert.Equal(settings.Width, loaded.Width);
        Assert.Equal(settings.Height, loaded.Height);
        Assert.Equal(settings.KeepOnTop, loaded.KeepOnTop);
    }

    [Fact]
    public void Load_ReturnsDefaults_WhenFileIsCorrupt()
    {
        var path = Path.Combine(_testDir, "settings.json");
        File.WriteAllText(path, "{ invalid json !!!");
        var settings = Settings.Load(_testDir);
        Assert.Equal("", settings.VideoPath);
    }
}
