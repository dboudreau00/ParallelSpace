using System;
using System.Threading;

namespace ParallelSpace.Services;

/// <summary>
/// Keeps one manager per Windows session. With a tray icon a second manager would
/// show a second icon and lose track of the first one's running copies, so a
/// second launch asks the first to show its window and exits.
/// </summary>
public sealed class SingleInstance : IDisposable
{
    private readonly Mutex? _mutex;
    private readonly EventWaitHandle? _show;
    private readonly ManualResetEvent _stop = new(false);
    private Thread? _listener;

    /// <summary>True when this process is the first (and only) manager.</summary>
    public bool IsFirst { get; }

    public SingleInstance(string name = "ParallelSpace")
    {
        try
        {
            // "Local\" scopes both names to this logon session.
            _mutex = new Mutex(true, $@"Local\{name}.Instance", out var created);
            _show = new EventWaitHandle(false, EventResetMode.AutoReset, $@"Local\{name}.Show");
            IsFirst = created;
        }
        catch (UnauthorizedAccessException)
        {
            // The first manager runs at another integrity level (for example elevated)
            // and owns the names. We can't signal it, but we must not start a second one.
            IsFirst = false;
        }
    }

    /// <summary>Asks the running manager to bring its window forward.</summary>
    public void SignalFirst()
    {
        try { _show?.Set(); } catch { }
    }

    /// <summary>Calls <paramref name="onShow"/> (on a worker thread) whenever another launch signals.</summary>
    public void Listen(Action onShow)
    {
        if (_show is null || _listener is not null)
            return;

        _listener = new Thread(() =>
        {
            var handles = new WaitHandle[] { _show, _stop };
            while (WaitHandle.WaitAny(handles) == 0)
                onShow();
        })
        {
            IsBackground = true,
            Name = "ParallelSpace.SingleInstance",
        };
        _listener.Start();
    }

    public void Dispose()
    {
        // Let the listener leave its wait before the handles it waits on go away.
        _stop.Set();
        _listener?.Join(1000);

        if (IsFirst)
        {
            try { _mutex?.ReleaseMutex(); } catch { }
        }
        _mutex?.Dispose();
        _show?.Dispose();
    }
}
