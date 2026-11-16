# Changelog

All notable changes to this project are documented here.
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/);
this project uses [Semantic Versioning](https://semver.org/).

## [1.2.0] — 2026-11-16

Parallel Space can now live in the background.

### Added
- **Startup page** with three switches, all off by default:
  **Start with Windows** (one entry in your own startup list; it opens hidden in the
  tray), **Minimize to the tray**, and **Keep running when the window is closed**.
- **Tray icon** with Open and Quit. It shows whenever a tray option is on, and always
  while the window is hidden, so a hidden window can't be lost.
- **Launch when Parallel Space starts**, per space, in the editor. It runs each time
  the manager opens, including at Windows startup.
- **One manager per Windows session.** Launching Parallel Space again brings the
  running window forward instead of opening a second manager.
- The Harness checks the startup entry and the single-instance guard under throwaway
  names, so the real entry is never touched.

### Changed
- `spaces.json` gains optional `MinimizeToTray`, `CloseToTray` and `LaunchOnStart`
  fields. Existing files load unchanged.
- SECURITY.md and the README describe the startup entry and the tray behaviour.

## [1.1.0] — 2026-08-28

Parallel Space became a general multi-app tool rather than a Discord one.

### Added
- **App catalog** — ready-made recipes for Chrome, Edge, Brave, Firefox, Telegram,
  Signal, Element, Slack, VS Code, Obsidian, qBittorrent, Thunderbird and the three
  Discord release channels. Installed apps are detected and can be added in one click.
- **Per-app techniques** — each recipe carries the trick that actually works for that
  app (profile flag, multi-instance flag, separate builds, or a redirected profile)
  and the UI names it, instead of assuming one approach fits everything.
- **`{DATA}` token** in a space's arguments, replaced at launch with that space's
  private folder — this is how a profile flag gets pointed somewhere isolated.
- **Six themes** (Midnight, Nord, Carbon, Ember, Daylight, Matrix) on a new
  **Appearance** page, switchable live. Every palette is checked for contrast:
  primary text ≥ 7:1 on every surface, secondary ≥ 4.5:1, white on accent ≥ 4.5:1.
- **Versioned install paths** — a recipe path may contain a `*` segment, so apps that
  live in a versioned folder are found (Edge 151's `EdgeCore\<version>\msedge.exe`).
- GitHub Actions for build and release, contributor guide, issue and PR templates.

### Changed
- Discord moved out of the front page into the catalog; the generic **Spaces** page
  is now the home screen.
- Space cards state their technique ("Profile flag · own profile") rather than an
  exe name.
- The whole UI is theme-driven: no hardcoded colours remain in the views.

## [1.0.0] — 2026-08-17

First release.

### Added
- Isolated app copies with per-space profiles, launch/close and live status.
- Discord multi-account through the official Stable / PTB / Canary channels.
- Add/edit dialog, Start-Menu app detection, packaged self-contained release.

### Fixed
- **Process tracking for stub launchers.** Apps that start through a bootstrapper
  that exits immediately (Squirrel/Electron updaters, Store app stubs) reported
  "Stopped" seconds after launch and could not be closed. Each launch now goes into
  its own Windows job object, and the process is created suspended so it is in the
  job before it runs — no race. Close terminates that copy's tree and nothing else.
- Config durability: atomic write-then-swap, an unreadable `spaces.json` is preserved
  as `.corrupt` instead of being overwritten, and write failures surface in the status
  bar instead of terminating the app.
- Glyph tiles were invisible — the alpha byte was in the wrong position for Avalonia's
  `#AARRGGBB`.
- The add/edit dialog's titlebar said "Edit space" even when adding.
- Release channels were only detected at startup, so installing one while the app was
  open still showed "Not installed".
- `AppScanner` could abort its whole scan on one inaccessible folder.
