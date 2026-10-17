# smolscren — Design Spec

**Date:** 2026-10-17
**Status:** Revised draft (v2) for review
**Author:** cuh (for HRD_Ophel)

---

## 1. Overview

smolscren is a Windows desktop application that plays a local video file in a small, always-on-top, transparent, click-through floating window. It is a **floating overlay** (not a true wallpaper behind the desktop icons): the video is visible above other windows, but mouse input passes through to whatever is underneath. A system tray icon and a global hotkey are the only interaction points while locked.

**Core use case:** User wants a small silent video looping in a corner of the screen that doesn't interfere with normal work.

---

## 2. Architecture

### 2.1 Stack

| Layer | Choice | Rationale |
|-------|--------|-----------|
| UI | WPF (.NET 8) | Hardware-accelerated video, native Windows integration |
| Video | MediaElement | Built-in, hardware-decoded, formats via OS codecs |
| Window mgmt | Win32 interop (P/Invoke) | Click-through, tool-window style, global hotkey |
| Tray | NotifyIcon (System.Windows.Forms) | Standard tray icon, no external deps |

**Single project**, no NuGet dependencies beyond the .NET SDK.

**Two-framework note:** Enabling WinForms (`UseWindowsForms`) alongside WPF creates ambiguous types (`Application`, `MessageBox`, `Point`, `Color`, etc.). Use explicit aliases or fully qualified names so `dotnet build` stays at zero warnings.

**DPI:** Include an app manifest declaring `PerMonitorV2` DPI awareness so the window renders correctly when dragged between monitors.

### 2.2 Process Model

Single process, single window. No background services, no network. Persistence is limited to `%APPDATA%\smolscren\settings.json`.

---

## 3. Components

### 3.1 MainWindow

| Property | Value | Notes |
|----------|-------|-------|
| WindowStyle | None | No border or title bar |
| AllowsTransparency | True | Transparent background (layered window) |
| Topmost | True (default) | Togglable from tray: **Keep on top** |
| ShowInTaskbar | False | No taskbar entry |
| Background | Transparent | |
| WindowState | Normal | |

### 3.2 VideoPlayer (MediaElement)

- `LoadedBehavior = Manual`, `UnloadedBehavior = Manual` (required to control `Play()` and `Position` from code)
- Set `Source`, then call `Play()` in code
- `Stretch = UniformToFill`
- `IsMuted = true`
- `MediaEnded` → `Position = TimeSpan.Zero; Play()` (loop forever)
- `MediaFailed` → error handling per §5

### 3.3 WindowStateManager

**Locked (default):**
- `WS_EX_TRANSPARENT` set: all mouse events pass through
- `WS_EX_TOOLWINDOW` set: no Alt+Tab entry
- The window receives no input, so **it cannot be unlocked from the window itself**

**Unlocked:**
- `WS_EX_TRANSPARENT` removed: window receives mouse events
- Drag via `DragMove()` on left mouse down
- Resize via corner grips (Thumb controls), with a visible border/outline so the user can tell it is unlocked
- Right-click → context menu: **Lock**, **Exit**

**Unlock/lock triggers (the only ones):**
1. Global hotkey, default `Ctrl+Alt+Shift+V`, registered with Win32 `RegisterHotKey` on the window's `HwndSource` (WPF key bindings need focus, which a click-through window never has). If registration fails (already taken), show a MessageBox once and rely on the tray.
2. Tray menu item **Unlock (move/resize)**, a checkable item whose checkmark shows the current state.
3. Tray icon double-click.

Unregister the hotkey on exit.

### 3.4 TrayIcon

- `NotifyIcon` in the system tray
- Icon or tooltip reflects state ("smolscren — locked" / "smolscren — unlocked")
- Right-click menu:
  - **Unlock (move/resize)** (checkable)
  - **Keep on top** (checkable, default on)
  - **Open video…**
  - **Exit**
- Double-click → toggle lock state
- Dispose the icon on exit so no ghost icon remains

### 3.5 Settings

`%APPDATA%\smolscren\settings.json` stores: last video path, window position and size, keep-on-top flag.

**Startup video resolution order:**
1. Command-line path argument (wins; **not** persisted)
2. Saved path from settings, if the file exists
3. OpenFileDialog

**Open video…** from the tray picks a new file, loads it immediately, and saves it as the last video path.

---

## 4. Data Flow

```
[Launch]
   │
   ▼
[Resolve video: CLI arg → settings.json → OpenFileDialog]
   │
   ▼
[MediaElement.Source = path; Play(); loop on MediaEnded]
   │
   ▼
[Window: click-through, topmost, transparent, silent]
   │
   ├─[Hotkey / tray Unlock / tray double-click]─► [Unlocked: drag, resize, right-click menu]
   │                                                   │
   │                                                   └─[same triggers or Lock]─► [Locked]
   ├─[Tray: Open video…]─► [Load new video, save path]
   └─[Tray: Exit]─► [Save window bounds, unregister hotkey, dispose tray, close]
```

---

## 5. Error Handling

| Scenario | Behavior |
|----------|----------|
| Video file not found | MessageBox, fall back to OpenFileDialog |
| Unsupported codec / `MediaFailed` | MessageBox with the exception message; offer to pick another file |
| settings.json corrupt | Delete and recreate with defaults, show OpenFileDialog |
| Video becomes unavailable during playback (e.g. network path dropped) | `MediaFailed` → MessageBox, exit gracefully |
| Hotkey already registered | One-time MessageBox; tray remains the way to unlock |

---

## 6. Testing

### 6.1 Manual Test Matrix

| # | Test | Expected |
|---|------|----------|
| 1 | Launch with no prior settings | OpenFileDialog appears |
| 2 | Select video | Video plays, loops, no audio |
| 3 | Click on the window area while locked | Click reaches the app or desktop icon underneath; video does not block input |
| 4 | Press the hotkey | Window unlocks (outline visible, tray checkmark on) |
| 5 | Drag unlocked window | Window moves |
| 6 | Resize via corner grip | Window resizes, video fills |
| 7 | Right-click unlocked window | Context menu with Lock and Exit |
| 8 | Click Exit | App closes, tray icon disappears |
| 9 | Tray right-click → Exit | App closes |
| 10 | Close and relaunch | Same video, position and size restored |
| 11 | Play a video from a network path or removable drive, then disconnect | MessageBox, graceful exit (local files are locked by playback and can't be deleted) |
| 12 | Launch with command-line path arg | That video loads; saved path unchanged |
| 13 | Tray → Open video… | New video loads and is remembered |
| 14 | Tray → Keep on top off | Other windows can cover the video |
| 15 | Hotkey already used by another app | One-time warning; tray unlock still works |
| 16 | Drag between monitors with different DPI | No blur or size jump |
| 17 | Play 1080p for several minutes | Smooth frame rate, no flicker (layered-window check) |

### 6.2 Build Verification

- `dotnet build`: zero warnings
- `dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true`: single .exe (WPF may still emit a few native DLLs alongside)

### 6.3 Known Risk

`AllowsTransparency=True` uses a layered window and can reduce video frame rate or cause flicker. Test #17 should run early. Fallback: a non-transparent window with `WS_EX_LAYERED` set manually.

---

## 7. Non-Goals

- No audio playback
- No playlist
- No video effects/filters
- No multi-monitor selection (window is dragged manually)
- No auto-start with Windows (user can add a shortcut to the Startup folder)
- No seeking controls
- No true wallpaper mode behind desktop icons (`WorkerW` parenting); this is a floating overlay

---

## 8. File Layout

```
D:\smolscren\
├── smolscren.csproj
├── app.manifest
├── App.xaml
├── App.xaml.cs
├── MainWindow.xaml
├── MainWindow.xaml.cs
├── WindowStateManager.cs
├── HotkeyManager.cs
├── TrayIcon.cs
├── Settings.cs
└── docs\
    └── superpowers\
        └── specs\
            └── 2026-10-17-smolscren-design.md
```

---

## 9. Open Questions

None blocking. Optional later: make the hotkey configurable in settings.json.