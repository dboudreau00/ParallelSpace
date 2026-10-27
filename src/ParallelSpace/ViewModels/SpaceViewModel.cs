using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ParallelSpace.Models;

namespace ParallelSpace.ViewModels;

/// <summary>UI wrapper around a single <see cref="Space"/>.</summary>
public sealed partial class SpaceViewModel : ObservableObject
{
    private readonly MainWindowViewModel _owner;

    public Space Model { get; }

    public SpaceViewModel(Space model, MainWindowViewModel owner)
    {
        Model = model;
        _owner = owner;
    }

    public string Name => Model.Name;
    public string Glyph => Model.Glyph;
    public string Accent => Model.Accent;

    public string Subtitle
    {
        get
        {
            if (!string.IsNullOrEmpty(Model.DiscordChannel))
                return "Separate build · own account";

            if (!string.IsNullOrEmpty(Model.PresetKey))
            {
                var preset = Services.AppCatalog.Presets
                    .FirstOrDefault(p => p.Key == Model.PresetKey);
                if (preset is not null)
                    return $"{preset.TechniqueLabel} · own profile";
            }

            if (Model.Isolation == IsolationMode.IsolatedProfile)
                return $"{ExeName} · isolated profile";

            return string.IsNullOrEmpty(ExeName) ? "custom app" : ExeName;
        }
    }

    private string ExeName =>
        string.IsNullOrEmpty(Model.ExecutablePath) ? "" : Path.GetFileName(Model.ExecutablePath);

    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private int _instances;

    public string StatusText => IsRunning
        ? (Instances > 1 ? $"Running · {Instances}" : "Running")
        : "Stopped";

    partial void OnIsRunningChanged(bool value)
    {
        OnPropertyChanged(nameof(StatusText));
        LaunchCommand.NotifyCanExecuteChanged();
        CloseCommand.NotifyCanExecuteChanged();
    }

    partial void OnInstancesChanged(int value) => OnPropertyChanged(nameof(StatusText));

    public void RefreshStatus(bool running, int instances)
    {
        IsRunning = running;
        Instances = instances;
    }

    /// <summary>Re-read display fields after the model is edited in the dialog.</summary>
    public void NotifyEdited()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Glyph));
        OnPropertyChanged(nameof(Accent));
        OnPropertyChanged(nameof(Subtitle));
    }

    [RelayCommand]
    private void Launch() => _owner.LaunchSpace(this);

    [RelayCommand]
    private Task Edit() => _owner.EditSpaceAsync(this);

    private bool CanClose() => IsRunning;

    [RelayCommand(CanExecute = nameof(CanClose))]
    private Task Close() => _owner.CloseSpace(this);

    [RelayCommand]
    private void OpenDataFolder() => _owner.OpenDataFolder(this);

    [RelayCommand]
    private Task Remove() => _owner.RemoveSpace(this);
}

