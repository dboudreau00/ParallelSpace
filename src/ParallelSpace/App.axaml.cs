using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Threading;
using ParallelSpace.Services;
using ParallelSpace.ViewModels;
using ParallelSpace.Views;

namespace ParallelSpace;

public partial class App : Application
{
    private IClassicDesktopStyleApplicationLifetime? _desktop;
    private MainWindow? _window;
    private MainWindowViewModel? _vm;
    private TrayIcon? _tray;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _desktop = desktop;

            var store = new SpaceStore();
            var launcher = new Launcher(store);

            // Paint the saved theme before the first window is shown.
            var vm = new MainWindowViewModel(store, launcher, new StartupRegistration());
            ThemeManager.Apply(this, ThemeManager.Resolve(store.ThemeKey));

            var window = new MainWindow { DataContext = vm };
            _vm = vm;
            _window = window;

            // Wire the add/edit dialog now that both window and view model exist.
            vm.ShowEditor = async editor =>
            {
                var dialog = new EditSpaceWindow { DataContext = editor };
                return await dialog.ShowDialog<bool>(window);
            };

            // The window can be hidden in the tray, so closing it is not what ends the
            // app. It ends when the window is really closed or the tray's Quit runs.
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            window.Closed += (_, _) => desktop.Shutdown();
            window.Closing += OnWindowClosing;
            window.PropertyChanged += OnWindowPropertyChanged;
            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is nameof(MainWindowViewModel.MinimizeToTray) or nameof(MainWindowViewModel.CloseToTray))
                    UpdateTrayVisibility();
            };

            BuildTray();

            // A startup launch begins in the tray; any other launch opens the window.
            var background = desktop.Args?.Contains(StartupRegistration.BackgroundArg) == true;
            if (!background)
                desktop.MainWindow = window;
            UpdateTrayVisibility();

            // A second launch asks this one to come forward.
            Program.Instance?.Listen(() => Dispatcher.UIThread.Post(ShowWindow));

            desktop.ShutdownRequested += (_, _) =>
            {
                _tray?.Dispose();
                launcher.Dispose();
            };

            Dispatcher.UIThread.Post(vm.LaunchStartupSpaces);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void BuildTray()
    {
        var open = new NativeMenuItem("Open Parallel Space");
        open.Click += (_, _) => ShowWindow();
        var quit = new NativeMenuItem("Quit");
        quit.Click += (_, _) => _desktop?.Shutdown();

        _tray = new TrayIcon
        {
            ToolTipText = "Parallel Space",
            Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://ParallelSpace/Assets/appicon.ico"))),
            Menu = new NativeMenu { open, new NativeMenuItemSeparator(), quit },
            IsVisible = false,
        };
        _tray.Clicked += (_, _) => ShowWindow();
        TrayIcon.SetIcons(this, new TrayIcons { _tray });
    }

    /// <summary>
    /// The icon shows whenever a tray option is on, and always while the window is
    /// hidden, so a hidden window can never be left with no way back to it.
    /// </summary>
    private void UpdateTrayVisibility()
    {
        if (_tray is null || _vm is null || _window is null)
            return;
        _tray.IsVisible = _vm.MinimizeToTray || _vm.CloseToTray || !_window.IsVisible;
    }

    private void ShowWindow()
    {
        if (_window is null)
            return;

        if (!_window.IsVisible)
            _window.Show();
        if (_window.WindowState == WindowState.Minimized)
            _window.WindowState = WindowState.Normal;
        if (_desktop is { MainWindow: null })
            _desktop.MainWindow = _window;

        _window.Activate();
        UpdateTrayVisibility();
    }

    private void HideToTray()
    {
        if (_window is null)
            return;

        _window.Hide();
        // Restore the state while hidden, or the next Show() would reopen minimized.
        _window.WindowState = WindowState.Normal;
        UpdateTrayVisibility();
    }

    private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        // Only the user's own close is turned into "hide". Quit, a system shutdown
        // and code-driven closes must go through, or Windows would wait on us.
        if (_vm?.CloseToTray == true && e.CloseReason == WindowCloseReason.WindowClosing && !e.IsProgrammatic)
        {
            e.Cancel = true;
            HideToTray();
        }
    }

    private void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Window.WindowStateProperty &&
            _window?.WindowState == WindowState.Minimized &&
            _vm?.MinimizeToTray == true)
        {
            // Not inside the state change itself.
            Dispatcher.UIThread.Post(HideToTray);
        }
    }
}
