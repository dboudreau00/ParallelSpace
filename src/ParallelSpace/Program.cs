using Avalonia;
using ParallelSpace.Services;
using System;
using System.Linq;

namespace ParallelSpace;

internal static class Program
{
    /// <summary>The single-instance guard for this process (null in the designer).</summary>
    internal static SingleInstance? Instance { get; private set; }

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called.
    [STAThread]
    public static int Main(string[] args)
    {
        using var instance = new SingleInstance();
        if (!instance.IsFirst)
        {
            // A launch from Windows startup must not pop the running window open.
            if (!args.Contains(StartupRegistration.BackgroundArg))
                instance.SignalFirst();
            return 0;
        }

        Instance = instance;
        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
