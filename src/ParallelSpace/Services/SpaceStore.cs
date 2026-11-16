using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using ParallelSpace.Models;

namespace ParallelSpace.Services;

/// <summary>
/// Loads/saves the list of spaces and owns the on-disk layout for per-space
/// isolated profiles. Everything lives under %APPDATA%\ParallelSpace.
/// </summary>
public sealed class SpaceStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    public string RootDir { get; }
    public string ConfigPath { get; }
    public string SpacesDataDir { get; }

    public SpaceStore()
    {
        RootDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ParallelSpace");
        ConfigPath = Path.Combine(RootDir, "spaces.json");
        SpacesDataDir = Path.Combine(RootDir, "Spaces");
        Directory.CreateDirectory(SpacesDataDir);
    }

    /// <summary>Non-null when the last Load() found a config it could not read.</summary>
    public string? LoadError { get; private set; }

    /// <summary>Selected theme key, round-tripped through the same config file.</summary>
    public string ThemeKey { get; set; } = "";

    /// <summary>Tray behaviour, round-tripped like <see cref="ThemeKey"/>.</summary>
    public bool MinimizeToTray { get; set; }
    public bool CloseToTray { get; set; }

    public List<Space> Load()
    {
        LoadError = null;
        try
        {
            if (!File.Exists(ConfigPath))
                return new List<Space>();

            var json = File.ReadAllText(ConfigPath);
            var config = JsonSerializer.Deserialize<SpaceConfig>(json, JsonOptions);
            ThemeKey = config?.ThemeKey ?? "";
            MinimizeToTray = config?.MinimizeToTray ?? false;
            CloseToTray = config?.CloseToTray ?? false;
            return config?.Spaces ?? new List<Space>();
        }
        catch (Exception ex)
        {
            // Keep the unreadable file instead of letting the next Save() erase it.
            LoadError = ex.Message;
            try { File.Move(ConfigPath, ConfigPath + ".corrupt", overwrite: true); } catch { }
            return new List<Space>();
        }
    }

    /// <summary>Writes the config atomically. Returns false instead of throwing.</summary>
    public bool Save(IEnumerable<Space> spaces)
    {
        try
        {
            var config = new SpaceConfig
            {
                Version = 1,
                ThemeKey = ThemeKey,
                MinimizeToTray = MinimizeToTray,
                CloseToTray = CloseToTray,
                Spaces = new List<Space>(spaces),
            };
            var json = JsonSerializer.Serialize(config, JsonOptions);
            Directory.CreateDirectory(RootDir);

            // Write-then-swap: an interrupted write can't truncate the live file.
            var tmp = ConfigPath + ".tmp";
            File.WriteAllText(tmp, json);
            File.Move(tmp, ConfigPath, overwrite: true);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Root data folder for a space's isolated profile.</summary>
    public string DataFolder(Space space)
    {
        var dir = Path.Combine(SpacesDataDir, Sanitize(space.Id));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string Sanitize(string s)
    {
        foreach (var ch in Path.GetInvalidFileNameChars())
            s = s.Replace(ch, '_');
        return string.IsNullOrWhiteSpace(s) ? "space" : s;
    }
}
