using System;
using System.IO;
using System.Text.Json;

namespace smolscren;

public class Settings
{
    public string VideoPath { get; set; } = "";
    public double Left { get; set; } = 100;
    public double Top { get; set; } = 100;
    public double Width { get; set; } = 400;
    public double Height { get; set; } = 300;
    public bool KeepOnTop { get; set; } = true;

    public static Settings Load()
    {
        return Load(null);
    }

    public static Settings Load(string? overrideDir)
    {
        try
        {
            var dir = overrideDir ?? GetSettingsDir();
            var path = Path.Combine(dir, "settings.json");
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<Settings>(json) ?? new Settings();
            }
        }
        catch { /* corrupt or missing — start fresh */ }
        return new Settings();
    }

    public void Save()
    {
        Save(null);
    }

    public void Save(string? overrideDir)
    {
        var dir = overrideDir ?? GetSettingsDir();
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "settings.json");
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    private static string GetSettingsDir()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "smolscren");
    }
}
