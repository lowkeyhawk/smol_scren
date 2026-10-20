# smolscren

A tiny always-on-top video overlay for Windows. It plays a local video file in a small,
borderless, transparent, **click-through** floating window — so a silent clip can loop in
the corner of your screen while your mouse input passes straight through to whatever is
underneath it.

A system tray icon and a global hotkey are the only ways to interact with it while locked.

> **Status: feature-complete, manual testing outstanding.** The spec and plan are
> implemented: project scaffold, settings persistence, window state (click-through lock /
> unlock), global hotkey, system tray, and the main window with video playback, drag,
> corner-grip resize, and context menu. It builds clean, the settings tests pass, and it
> publishes a single-file `.exe`.
>
> What is left is the **manual test matrix** (spec §6.1) — that needs a human at the
> keyboard to judge playback smoothness and click-through feel. The plan document linked
> under [Development](#development) is the source of truth for what is done.

## What it does

- **Click-through overlay** — `WS_EX_TRANSPARENT` means clicks and drags land on the app or
  desktop icon underneath, never on the video
- **Always on top** — optional, togglable from the tray
- **No taskbar or Alt+Tab presence** — `WS_EX_TOOLWINDOW` plus `ShowInTaskbar = false`
- **Silent, seamless looping** — `MediaElement` with `IsMuted`, restarting on `MediaEnded`
- **Lock / unlock** — unlock to drag and resize, then lock it back down
- **Remembers everything** — last video, window position and size, keep-on-top flag
- **Command-line friendly** — `smolscren.exe D:\clips\loop.mp4` skips the file picker

## Requirements

- Windows 10 or later
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) to build (a published
  self-contained `.exe` needs no runtime installed)
- Video playback relies on the OS codecs, so any format Windows Media Player can play,
  smolscren can play

## Build

```sh
dotnet build
```

Run the tests for the settings layer:

```sh
dotnet test Settings.Tests
```

## Usage

Once the window and tray layers are in place, the intended interaction model is:

| Action | How |
|--------|-----|
| Unlock (move / resize) | Global hotkey `Ctrl+Alt+Shift+V`, tray menu, or tray double-click |
| Lock again | Same hotkey, or right-click the unlocked window → **Lock** |
| Move | Drag the unlocked window |
| Resize | Drag a corner grip (the window shows an outline while unlocked) |
| Open a different video | Tray → **Open video…** |
| Toggle always-on-top | Tray → **Keep on top** |
| Quit | Tray → **Exit**, or right-click the unlocked window → **Exit** |

The default hotkey is `Ctrl+Alt+Shift+V`. If another app has already claimed it, smolscren
warns once and falls back to the tray menu for unlocking.

## Settings

Stored as JSON at `%APPDATA%\smolscren\settings.json`:

```json
{
  "VideoPath": "D:\\clips\\loop.mp4",
  "Left": 100,
  "Top": 100,
  "Width": 400,
  "Height": 300,
  "KeepOnTop": true
}
```

Startup resolves which video to play in this order:

1. A path passed on the command line (wins, and is **not** persisted)
2. The saved `VideoPath`, if that file still exists
3. An open-file dialog

If the settings file is corrupt or unreadable, it is treated as absent and defaults are used.

## Publishing

A single self-contained executable:

```sh
dotnet publish -c Release -r win-x64 --self-contained \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true
```

WPF may still emit a few native DLLs next to the `.exe`; that is expected.

## Project layout

```
smolscren.csproj        WPF app (net8.0-windows, WPF + WinForms for the tray icon)
app.manifest            PerMonitorV2 DPI awareness
App.xaml / App.xaml.cs  Application entry, command-line video path
Settings.cs             JSON persistence of video path and window bounds
WindowStateManager.cs   Win32 click-through / tool-window lock and unlock
Settings.Tests/         xUnit tests for the settings layer
docs/superpowers/       Design spec and implementation plan
```

## Development

This project is built from a written spec and a task-by-task implementation plan:

- [`docs/superpowers/specs/2026-10-17-smolscren-design.md`](docs/superpowers/specs/2026-10-17-smolscren-design.md)
  — architecture, components, data flow, error handling, and a 17-case manual test matrix
- [`docs/superpowers/plans/2026-10-17-smolscren.md`](docs/superpowers/plans/2026-10-17-smolscren.md)
  — the implementation plan and current task breakdown

Read the spec before changing behavior; read the plan before starting new work.

## Known limitations

- **Not a wallpaper.** This is a floating overlay above other windows, not a layer behind
  the desktop icons. `WorkerW` parenting is explicitly out of scope.
- **No audio, no playlists, no seeking, no effects.** Silent looping playback is the whole
  feature set.
- **Transparency has a cost.** `AllowsTransparency` uses a layered window, which can reduce
  frame rate or cause flicker at high resolutions. The spec flags this as the main
  performance risk, with a manual `WS_EX_LAYERED` fallback if it bites.
- **No auto-start.** Drop a shortcut in your Startup folder if you want it on login.
- **Multi-monitor** placement is manual — drag it where you want it. Per-monitor DPI is
  handled, so moving between displays with different scaling will not blur it.

## License

[Unlicense](LICENSE) — public domain. Do whatever you want with it.
