using System;
using System.Collections.Generic;

namespace ParallelSpace.Models;

/// <summary>
/// A single "parallel space": one launchable, isolated clone of an app.
/// This is the persisted shape (serialized to spaces.json).
/// </summary>
public sealed class Space
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Friendly label shown in the UI, e.g. "Discord — Main".</summary>
    public string Name { get; set; } = "New space";

    /// <summary>Absolute path to the executable to launch.</summary>
    public string ExecutablePath { get; set; } = "";

    /// <summary>Extra command-line arguments appended on launch (e.g. --multi-instance).</summary>
    public string Arguments { get; set; } = "";

    /// <summary>Optional working directory. Defaults to the exe's folder when empty.</summary>
    public string WorkingDirectory { get; set; } = "";

    public IsolationMode Isolation { get; set; } = IsolationMode.None;

    /// <summary>
    /// When Isolation == IsolatedProfile: also append
    /// <c>--user-data-dir="&lt;dataFolder&gt;"</c>. Helpful for Chromium/Electron apps.
    /// </summary>
    public bool AppendUserDataDir { get; set; } = false;

    /// <summary>Free-form tag used for grouping / preset identity (e.g. "discord").</summary>
    public string Kind { get; set; } = "custom";

    /// <summary>
    /// For Discord channel spaces: "stable" | "ptb" | "canary". Empty otherwise.
    /// Lets the launcher resolve/refresh the versioned exe at run time.
    /// </summary>
    public string DiscordChannel { get; set; } = "";

    /// <summary>Launch this space whenever Parallel Space starts (unless it is already running).</summary>
    public bool LaunchOnStart { get; set; } = false;

    /// <summary>Catalog preset this space came from (see AppCatalog). Empty for hand-added apps.</summary>
    public string PresetKey { get; set; } = "";

    /// <summary>Emoji/glyph shown on the card. Purely cosmetic.</summary>
    public string Glyph { get; set; } = "📦";

    /// <summary>Accent color (hex) for the card stripe. Purely cosmetic.</summary>
    public string Accent { get; set; } = "#5865F2";
}

/// <summary>Root of the persisted config file.</summary>
public sealed class SpaceConfig
{
    public int Version { get; set; } = 1;

    /// <summary>Selected theme key (see AppThemes). Empty means the default.</summary>
    public string ThemeKey { get; set; } = "";

    /// <summary>Minimizing hides the window in the tray instead of the taskbar.</summary>
    public bool MinimizeToTray { get; set; } = false;

    /// <summary>The close button hides the window in the tray; Quit in the tray menu exits.</summary>
    public bool CloseToTray { get; set; } = false;

    public List<Space> Spaces { get; set; } = new();
}
