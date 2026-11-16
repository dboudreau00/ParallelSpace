using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ParallelSpace.Models;
using ParallelSpace.Services;

namespace ParallelSpace.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly SpaceStore _store;
    private readonly Launcher _launcher;
    private readonly StartupRegistration _startup;
    private readonly DispatcherTimer _timer;

    // Set while a toggle is being put back, so the change handler doesn't run again.
    private bool _restoring;

    public ObservableCollection<SpaceViewModel> Spaces { get; } = new();
    public ObservableCollection<CatalogItemViewModel> Catalog { get; } = new();
    public ObservableCollection<DetectedApp> DetectedApps { get; } = new();
    public ObservableCollection<ThemeOptionViewModel> Themes { get; } = new();

    /// <summary>Set by the app: shows the add/edit dialog and returns true if saved.</summary>
    public Func<EditSpaceViewModel, Task<bool>>? ShowEditor { get; set; }

    [ObservableProperty] private string _statusMessage = "Ready.";
    [ObservableProperty] private bool _hasDetectedApps;
    [ObservableProperty] private NavPage _selectedPage = NavPage.Spaces;

    public bool IsSpacesPage => SelectedPage == NavPage.Spaces;
    public bool IsCatalogPage => SelectedPage == NavPage.Catalog;
    public bool IsAppearancePage => SelectedPage == NavPage.Appearance;
    public bool IsStartupPage => SelectedPage == NavPage.Startup;

    partial void OnSelectedPageChanged(NavPage value)
    {
        OnPropertyChanged(nameof(IsSpacesPage));
        OnPropertyChanged(nameof(IsCatalogPage));
        OnPropertyChanged(nameof(IsAppearancePage));
        OnPropertyChanged(nameof(IsStartupPage));
    }

    [RelayCommand] private void ShowSpaces() => SelectedPage = NavPage.Spaces;
    [RelayCommand] private void ShowCatalog() => SelectedPage = NavPage.Catalog;
    [RelayCommand] private void ShowAppearance() => SelectedPage = NavPage.Appearance;
    [RelayCommand] private void ShowStartup() => SelectedPage = NavPage.Startup;

    public bool HasSpaces => Spaces.Count > 0;
    public int SpaceCount => Spaces.Count;

    public MainWindowViewModel(SpaceStore store, Launcher launcher, StartupRegistration startup)
    {
        _store = store;
        _launcher = launcher;
        _startup = startup;
        _launcher.InstancesChanged += OnInstancesChanged;

        BuildCatalog();

        foreach (var model in _store.Load())
            Spaces.Add(new SpaceViewModel(model, this));
        Spaces.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasSpaces));
            OnPropertyChanged(nameof(SpaceCount));
        };

        // Backing fields, not the properties: loading must not run the change handlers.
        _minimizeToTray = _store.MinimizeToTray;
        _closeToTray = _store.CloseToTray;
        _startWithWindows = _startup.IsEnabled();
        if (_startWithWindows)
            _startup.Refresh();

        BuildThemes();
        RefreshCatalogStates();
        RefreshStatuses();

        if (_store.LoadError is not null)
            StatusMessage = "Couldn't read spaces.json — it was kept as spaces.json.corrupt.";

        // Re-check installs too, so an app installed while we're open stops
        // showing "Not installed".
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _timer.Tick += (_, _) =>
        {
            RefreshStatuses();
            RefreshCatalogStates();
        };
        _timer.Start();

        _ = LoadDetectedAppsAsync();
    }

    // ---- Catalog ---------------------------------------------------------------

    private void BuildCatalog()
    {
        foreach (var p in AppCatalog.Presets)
        {
            Catalog.Add(new CatalogItemViewModel
            {
                Key = p.Key,
                Display = p.Display,
                Category = p.Category,
                Glyph = p.Glyph,
                Accent = p.Accent,
                TechniqueLabel = p.TechniqueLabel,
                Note = p.Note,
                DownloadUrl = p.DownloadUrl,
                OnActivate = ActivateCatalogItem,
            });
        }

        // Discord needs its own row per release channel — that is what gives each
        // one a separate account.
        foreach (var ch in DiscordDetector.Channels)
        {
            Catalog.Add(new CatalogItemViewModel
            {
                Key = "discord-" + ch.Key,
                Display = ch.Display,
                Category = "Chat",
                Glyph = ch.Glyph,
                Accent = ch.Accent,
                TechniqueLabel = "Separate builds",
                Note = "Each Discord channel keeps its own account. Add two or three for multiple logins.",
                DownloadUrl = ch.DownloadUrl,
                DiscordChannel = ch.Key,
                OnActivate = ActivateCatalogItem,
            });
        }
    }

    private void ActivateCatalogItem(CatalogItemViewModel item)
    {
        if (!item.IsInstalled)
        {
            if (!string.IsNullOrEmpty(item.DownloadUrl))
            {
                OpenUrl(item.DownloadUrl);
                StatusMessage = $"Opened the {item.Display} download page. Install it, then click Add space.";
            }
            else
            {
                StatusMessage = $"{item.Display} isn't installed — install it, then click Add space.";
            }
            return;
        }

        Space space;

        if (!string.IsNullOrEmpty(item.DiscordChannel))
        {
            var ch = DiscordDetector.Get(item.DiscordChannel);
            if (ch is null)
                return;

            space = new Space
            {
                Name = ch.Display,
                Kind = "discord",
                DiscordChannel = ch.Key,
                Isolation = IsolationMode.None,
                AppendUserDataDir = false,
                Glyph = ch.Glyph,
                Accent = ch.Accent,
            };
        }
        else
        {
            var preset = AppCatalog.Presets.FirstOrDefault(p => p.Key == item.Key);
            if (preset is null)
                return;

            var exe = AppCatalog.ResolveExe(preset);
            if (exe is null)
            {
                StatusMessage = $"{item.Display} looks installed but its program file moved.";
                return;
            }

            // A preset can be added once per account; number the repeats.
            var existing = Spaces.Count(sp => sp.Model.PresetKey == preset.Key);
            var name = existing == 0 ? preset.Display : $"{preset.Display} {existing + 1}";

            space = new Space
            {
                Name = name,
                Kind = "preset",
                PresetKey = preset.Key,
                ExecutablePath = exe,
                Arguments = preset.Arguments,
                // A preset that carries its own profile flag needs no environment
                // redirect; the flag is both more reliable and less invasive.
                Isolation = preset.Technique == MultiTechnique.IsolatedProfile
                    ? IsolationMode.IsolatedProfile
                    : IsolationMode.None,
                AppendUserDataDir = false,
                Glyph = preset.Glyph,
                Accent = preset.Accent,
            };
        }

        AddSpace(space);
        StatusMessage = $"Added {space.Name}. Launch it to start a separate copy.";
    }

    private void RefreshCatalogStates()
    {
        foreach (var item in Catalog)
        {
            if (!string.IsNullOrEmpty(item.DiscordChannel))
            {
                var ch = DiscordDetector.Get(item.DiscordChannel);
                item.IsInstalled = ch is not null && DiscordDetector.IsInstalled(ch);
                item.IsAdded = Spaces.Any(s => s.Model.DiscordChannel == item.DiscordChannel);
            }
            else
            {
                var preset = AppCatalog.Presets.FirstOrDefault(p => p.Key == item.Key);
                item.IsInstalled = preset is not null && AppCatalog.IsInstalled(preset);
                item.IsAdded = Spaces.Any(s => s.Model.PresetKey == item.Key);
            }
        }
    }

    // ---- Themes ----------------------------------------------------------------

    private void BuildThemes()
    {
        foreach (var theme in AppThemes.All)
            Themes.Add(new ThemeOptionViewModel(theme) { OnActivate = ApplyTheme });

        var current = ThemeManager.Resolve(_store.ThemeKey);
        MarkSelectedTheme(current.Key);
    }

    private void ApplyTheme(ThemeOptionViewModel option)
    {
        if (Application.Current is { } app)
            ThemeManager.Apply(app, option.Theme);

        MarkSelectedTheme(option.Key);
        _store.ThemeKey = option.Key;
        if (Save())
            StatusMessage = $"Theme set to {option.Name}.";
    }

    private void MarkSelectedTheme(string key)
    {
        foreach (var t in Themes)
            t.IsSelected = string.Equals(t.Key, key, StringComparison.OrdinalIgnoreCase);
    }

    // ---- Startup and background -------------------------------------------------

    [ObservableProperty] private bool _startWithWindows;
    [ObservableProperty] private bool _minimizeToTray;
    [ObservableProperty] private bool _closeToTray;

    /// <summary>False when running from a build with no ParallelSpace.exe to register.</summary>
    public bool CanRegisterStartup => _startup.CanRegister;

    partial void OnStartWithWindowsChanged(bool value)
    {
        if (_restoring)
            return;

        if (_startup.SetEnabled(value, out var error))
        {
            StatusMessage = value
                ? "Parallel Space will start with Windows, in the tray."
                : "Parallel Space no longer starts with Windows.";
            return;
        }

        // Show what is actually registered, not what was clicked.
        _restoring = true;
        StartWithWindows = !value;
        _restoring = false;
        StatusMessage = $"Couldn't change the startup entry: {error}";
    }

    partial void OnMinimizeToTrayChanged(bool value)
    {
        _store.MinimizeToTray = value;
        if (Save())
            StatusMessage = value
                ? "Minimizing now hides Parallel Space in the tray."
                : "Minimizing now uses the taskbar.";
    }

    partial void OnCloseToTrayChanged(bool value)
    {
        _store.CloseToTray = value;
        if (Save())
            StatusMessage = value
                ? "Closing the window now keeps Parallel Space running in the tray."
                : "Closing the window now quits Parallel Space.";
    }

    /// <summary>
    /// Launches every space marked "Launch when Parallel Space starts" that this
    /// launcher doesn't already see running. Called once, after the window exists.
    /// </summary>
    public void LaunchStartupSpaces()
    {
        int started = 0;
        var failures = new System.Collections.Generic.List<string>();

        foreach (var vm in Spaces.Where(s => s.Model.LaunchOnStart))
        {
            if (_launcher.GetStatus(vm.Model).Running)
                continue;

            var result = _launcher.Launch(vm.Model);
            if (result.Ok)
                started++;
            else
                failures.Add($"{vm.Name} ({result.Error})");
        }

        RefreshStatuses();
        if (failures.Count > 0)
            StatusMessage = $"Couldn't launch on start: {string.Join("; ", failures)}";
        else if (started > 0)
            StatusMessage = started == 1 ? "Launched 1 space on start." : $"Launched {started} spaces on start.";
    }

    // ---- Detected apps ---------------------------------------------------------

    private async Task LoadDetectedAppsAsync()
    {
        try
        {
            var apps = await Task.Run(AppScanner.Scan);
            foreach (var app in apps)
                DetectedApps.Add(app);
            HasDetectedApps = DetectedApps.Count > 0;
        }
        catch
        {
            // Detection is a convenience; ignore failures.
        }
    }

    // ---- Adding / editing ------------------------------------------------------

    /// <summary>"+ Add app…" — opens an empty editor to configure a new space.</summary>
    [RelayCommand]
    private Task AddNew() => AddViaDialogAsync(null, null);

    /// <summary>Quick-add from the detected-apps strip: opens the editor prefilled.</summary>
    [RelayCommand]
    private Task AddDetectedApp(DetectedApp? app) =>
        app is null ? Task.CompletedTask : AddViaDialogAsync(app.ExePath, app.Name);

    private async Task AddViaDialogAsync(string? exePath, string? name)
    {
        if (ShowEditor is null)
        {
            StatusMessage = "Editor unavailable (dialog not wired).";
            return;
        }

        var space = new Space
        {
            Name = name ?? (string.IsNullOrEmpty(exePath) ? "" : Path.GetFileNameWithoutExtension(exePath)),
            ExecutablePath = exePath ?? "",
            Kind = "custom",
            Isolation = IsolationMode.IsolatedProfile,
            AppendUserDataDir = false,   // opt-in: plain Win32 apps treat it as a file to open
            Glyph = "📦",
            Accent = "#4F5BD5",
        };

        try
        {
            var editor = new EditSpaceViewModel(space, isExisting: false);
            if (await ShowEditor(editor))   // ApplyToModel runs inside the dialog on Save
            {
                AddSpace(space);
                StatusMessage = $"Added \"{space.Name}\". Launch it to run an isolated copy.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Editor error: {ex.Message}";
        }
    }

    /// <summary>Edit an existing space via the dialog.</summary>
    public async Task EditSpaceAsync(SpaceViewModel vm)
    {
        if (ShowEditor is null)
            return;

        var editor = new EditSpaceViewModel(vm.Model, isExisting: true);
        if (await ShowEditor(editor))
        {
            Save();
            vm.NotifyEdited();
            RefreshStatuses();
            StatusMessage = $"Saved changes to {vm.Model.Name}.";
        }
    }

    private void AddSpace(Space space)
    {
        Spaces.Add(new SpaceViewModel(space, this));
        Save();
        RefreshCatalogStates();
        RefreshStatuses();
        SelectedPage = NavPage.Spaces;
    }

    // ---- Per-space actions (invoked from SpaceViewModel) -----------------------

    public void LaunchSpace(SpaceViewModel vm)
    {
        var result = _launcher.Launch(vm.Model);
        StatusMessage = result.Ok
            ? $"Launched {vm.Name}."
            : $"Couldn't launch {vm.Name}: {result.Error}";
        RefreshStatuses();
    }

    public void CloseSpace(SpaceViewModel vm)
    {
        _launcher.CloseAll(vm.Model);
        StatusMessage = $"Closed {vm.Name}.";
        RefreshStatuses();
    }

    public void RemoveSpace(SpaceViewModel vm)
    {
        var index = Spaces.IndexOf(vm);
        Spaces.Remove(vm);

        // Put it back if the write failed, so the list never diverges from disk.
        if (!_store.Save(Spaces.Select(s => s.Model)))
        {
            Spaces.Insert(Math.Max(0, index), vm);
            StatusMessage = "Couldn't write spaces.json — nothing was removed.";
            return;
        }

        RefreshCatalogStates();
        StatusMessage = $"Removed {vm.Name} from the list (its data folder was kept).";
    }

    public void OpenDataFolder(SpaceViewModel vm)
    {
        string folder = !string.IsNullOrEmpty(vm.Model.DiscordChannel)
            ? DiscordDetector.DataPath(DiscordDetector.Get(vm.Model.DiscordChannel)!)
            : _store.DataFolder(vm.Model);

        try
        {
            Directory.CreateDirectory(folder);
            Process.Start(new ProcessStartInfo { FileName = folder, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            StatusMessage = $"Couldn't open folder: {ex.Message}";
        }
    }

    // ---- Housekeeping ----------------------------------------------------------

    private void OnInstancesChanged() => Dispatcher.UIThread.Post(RefreshStatuses);

    private void RefreshStatuses()
    {
        foreach (var vm in Spaces)
        {
            var status = _launcher.GetStatus(vm.Model);
            vm.RefreshStatus(status.Running, status.Instances);
        }
    }

    private bool Save()
    {
        if (_store.Save(Spaces.Select(s => s.Model)))
            return true;

        StatusMessage = "Couldn't write spaces.json — your changes may not persist.";
        return false;
    }

    private void OpenUrl(string url)
    {
        try { Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true }); }
        catch (Exception ex) { StatusMessage = $"Couldn't open browser: {ex.Message}"; }
    }
}
