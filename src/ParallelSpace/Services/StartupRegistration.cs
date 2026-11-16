using System;
using System.IO;
using Microsoft.Win32;

namespace ParallelSpace.Services;

/// <summary>
/// "Start with Windows": a single value under the current user's Run key.
/// Nothing is written machine-wide, no task or service is created, and no
/// elevation is needed. Windows' own Settings / Task Manager list it and can
/// switch it off, and we honour that.
/// </summary>
public sealed class StartupRegistration
{
    /// <summary>Passed on the startup command line: begin in the tray, no window.</summary>
    public const string BackgroundArg = "--background";

    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    // Task Manager's Startup tab keeps its on/off switch here. The first byte is
    // even when the entry is enabled and odd when the user disabled it.
    private const string ApprovedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

    private readonly string _valueName;
    private readonly string? _exePath;

    /// <param name="valueName">Registry value name. Tests pass their own so they never touch the real entry.</param>
    /// <param name="exePath">Program to start. Defaults to this process's exe.</param>
    public StartupRegistration(string valueName = "ParallelSpace", string? exePath = null)
    {
        _valueName = valueName;
        _exePath = exePath ?? Environment.ProcessPath;
    }

    /// <summary>
    /// False when there is no real exe to point at, e.g. under "dotnet run", where the
    /// process is dotnet.exe and a startup entry would launch the wrong thing.
    /// </summary>
    public bool CanRegister =>
        !string.IsNullOrEmpty(_exePath) &&
        File.Exists(_exePath) &&
        !string.Equals(Path.GetFileNameWithoutExtension(_exePath), "dotnet", StringComparison.OrdinalIgnoreCase);

    private string Command => $"\"{_exePath}\" {BackgroundArg}";

    /// <summary>True when the entry exists and Windows has not been told to skip it.</summary>
    public bool IsEnabled()
    {
        try
        {
            using var run = Registry.CurrentUser.OpenSubKey(RunKey);
            if (run?.GetValue(_valueName) is not string)
                return false;

            using var approved = Registry.CurrentUser.OpenSubKey(ApprovedKey);
            return !(approved?.GetValue(_valueName) is byte[] { Length: > 0 } flags && (flags[0] & 1) == 1);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Adds or removes the entry. Returns false with a reason instead of throwing.</summary>
    public bool SetEnabled(bool enabled, out string? error)
    {
        error = null;
        try
        {
            if (enabled)
            {
                if (!CanRegister)
                {
                    error = "This copy isn't running from a ParallelSpace.exe, so there is nothing to start.";
                    return false;
                }

                using var run = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
                run.SetValue(_valueName, Command, RegistryValueKind.String);

                // A leftover "disabled" flag from Task Manager would keep it off.
                using var approved = Registry.CurrentUser.OpenSubKey(ApprovedKey, writable: true);
                approved?.DeleteValue(_valueName, throwOnMissingValue: false);
            }
            else
            {
                using var run = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
                run?.DeleteValue(_valueName, throwOnMissingValue: false);
            }
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Re-points an existing entry at the current exe, so moving or updating the
    /// program doesn't leave Windows starting a path that is gone.
    /// </summary>
    public void Refresh()
    {
        try
        {
            if (!CanRegister)
                return;

            using var run = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (run?.GetValue(_valueName) is string current &&
                !string.Equals(current, Command, StringComparison.OrdinalIgnoreCase))
            {
                run.SetValue(_valueName, Command, RegistryValueKind.String);
            }
        }
        catch
        {
            // Best effort; the toggle still reflects what is actually registered.
        }
    }
}
