# Parallel Space v1.2.0

**Run isolated, parallel copies of any Windows app** — now from the tray. This release
adds a Startup page so Parallel Space can start with Windows and stay out of the way.

## ✨ What's new

- **Start with Windows.** Opens Parallel Space when you sign in, hidden in the tray.
  It adds one entry to your own startup list and removes it when you switch it off.
- **Minimize to the tray.** Minimizing hides the window instead of using the taskbar.
- **Keep running when the window is closed.** The close button hides the window; **Quit**
  in the tray menu exits.
- **Launch when Parallel Space starts**, per space — tick it in the ✎ editor.
- **One manager per session.** Launching it again brings the running window forward.

All of it is off until you turn it on. Keeping the manager in the tray also keeps Close and
the status dots accurate, because it tracks the copies it launched.

## 🔧 Changed

- `spaces.json` gains optional `MinimizeToTray`, `CloseToTray` and `LaunchOnStart` fields;
  existing files load unchanged.
- SECURITY.md names the one registry value the startup option writes.

## 📦 Install

1. Download **`ParallelSpace-v1.2.0-win-x64.zip`** below.
2. Unzip and run **`ParallelSpace.exe`**.

Windows 10/11 (x64), no .NET runtime and no admin rights needed. Not code-signed, so
choose **More info → Run anyway** if SmartScreen prompts.
