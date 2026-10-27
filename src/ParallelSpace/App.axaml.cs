using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ParallelSpace.Services;
using ParallelSpace.ViewModels;
using ParallelSpace.Views;
using System.Threading.Tasks;

namespace ParallelSpace;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var store = new SpaceStore();
            var launcher = new Launcher(store);

            // Paint the saved theme before the first window is shown.
            var vm = new MainWindowViewModel(store, launcher);
            ThemeManager.Apply(this, ThemeManager.Resolve(store.ThemeKey));

            var window = new MainWindow { DataContext = vm };

            // Wire dialogs now that both window and view model exist.
            vm.ShowEditor = async editor =>
            {
                var dialog = new EditSpaceWindow { DataContext = editor };
                return await dialog.ShowDialog<bool>(window);
            };
            vm.ShowConfirmation = async (title, message) =>
            {
                var dialog = new ConfirmActionWindow(title, message);
                return await dialog.ShowDialog<bool>(window);
            };

            desktop.MainWindow = window;
            desktop.ShutdownRequested += (_, _) => launcher.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }
}

