using System.Diagnostics;
using ParallelSpace.Models;
using ParallelSpace.Services;

// Functional check of the real service layer (no UI).
// 1) Discord channel detection + exe resolution
// 2) Isolated-profile launch of a harmless app (notepad): env redirect,
//    folder creation, process tracking, status, close.

Console.WriteLine("=== Discord channel detection ===");
foreach (var ch in DiscordDetector.Channels)
{
    var installed = DiscordDetector.IsInstalled(ch);
    var exe = DiscordDetector.ResolveExe(ch);
    Console.WriteLine($"  {ch.Display,-16} installed={installed,-5} exe={exe ?? "(none)"}");
    Console.WriteLine($"  {"",16} data ={DiscordDetector.DataPath(ch)}");
}

// Target defaults to notepad; pass an exe to exercise a specific launch pattern
// (e.g. the stub-then-child pattern that job tracking exists to handle).
var target = args.Length > 0 ? args[0] : Path.Combine(Environment.SystemDirectory, "notepad.exe");
var targetName = Path.GetFileNameWithoutExtension(target);

Console.WriteLine();
Console.WriteLine($"=== Isolated-profile launch ({targetName}) ===");

var store = new SpaceStore();
var launcher = new Launcher(store);

var space = new Space
{
    Name = $"{targetName} (isolated test)",
    ExecutablePath = target,
    Isolation = IsolationMode.IsolatedProfile,
    AppendUserDataDir = false, // plain exes choke on unknown args
    Kind = "custom",
};

var dataFolder = store.DataFolder(space);
Console.WriteLine($"  data folder: {dataFolder}");

var result = launcher.Launch(space);
Console.WriteLine($"  launch ok={result.Ok} error={result.Error ?? "-"}");

Thread.Sleep(2000);
var status = launcher.GetStatus(space);
Console.WriteLine($"  status running={status.Running} instances={status.Instances}");

var home = Path.Combine(dataFolder, "Home");
var roaming = Path.Combine(home, "AppData", "Roaming");
Console.WriteLine($"  redirected profile tree created: Home={Directory.Exists(home)} Roaming={Directory.Exists(roaming)}");

var live = Process.GetProcessesByName(targetName).Length;
Console.WriteLine($"  '{targetName}' processes on box: {live}");

launcher.CloseAll(space);
Thread.Sleep(1500);
var after = launcher.GetStatus(space);
var liveAfter = Process.GetProcessesByName(targetName).Length;
Console.WriteLine($"  after close running={after.Running} instances={after.Instances} processes={liveAfter}");

// Leave no test profile behind.
launcher.Dispose();
try { Directory.Delete(dataFolder, recursive: true); } catch { }

// Startup entry and single-instance guard, under throwaway names so the real
// "ParallelSpace" Run value and instance mutex are never touched.
Console.WriteLine();
Console.WriteLine("=== Startup entry + single instance ===");

var failed = false;
void Check(string what, bool ok)
{
    Console.WriteLine($"  {(ok ? "PASS" : "FAIL")}  {what}");
    if (!ok) failed = true;
}

const string testValue = "ParallelSpace.HarnessTest";
var testExe = Environment.ProcessPath!;
var reg = new StartupRegistration(testValue, testExe);
try
{
    Check("a real exe can be registered", reg.CanRegister);
    Check("dotnet.exe is refused", !new StartupRegistration(testValue, Path.Combine(AppContext.BaseDirectory, "dotnet.exe")).CanRegister);
    Check("not enabled to begin with", !reg.IsEnabled());

    Check("enable succeeds", reg.SetEnabled(true, out var enableError) && enableError is null);
    Check("enabled after enable", reg.IsEnabled());

    using (var run = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))
        Check("command is quoted exe + --background",
            run?.GetValue(testValue) as string == $"\"{testExe}\" {StartupRegistration.BackgroundArg}");

    Check("disable succeeds", reg.SetEnabled(false, out _));
    Check("gone after disable", !reg.IsEnabled());
}
finally
{
    reg.SetEnabled(false, out _);
}

using (var first = new SingleInstance("ParallelSpace.HarnessTest"))
{
    Check("first instance owns the guard", first.IsFirst);

    using var second = new SingleInstance("ParallelSpace.HarnessTest");
    Check("second instance is told it is not first", !second.IsFirst);

    using var shown = new ManualResetEventSlim(false);
    first.Listen(shown.Set);
    second.SignalFirst();
    Check("second launch signals the first", shown.Wait(2000));
}

Console.WriteLine();
Console.WriteLine(failed ? "FAILED" : "DONE");
if (failed) Environment.ExitCode = 1;
