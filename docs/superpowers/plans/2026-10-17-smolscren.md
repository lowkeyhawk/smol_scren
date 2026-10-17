# smolscren Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a Windows desktop app that plays a local video in a small, always-on-top, transparent, click-through floating window with system tray control.

**Architecture:** Single WPF (.NET 8) project. Win32 P/Invoke for click-through and global hotkey. WinForms NotifyIcon for system tray. Settings persisted as JSON in %APPDATA%. No NuGet dependencies.

**Tech Stack:** C#, WPF, .NET 8, Win32 interop, WinForms (NotifyIcon)

**Spec:** `docs/superpowers/specs/2026-10-17-smolscren-design.md`

---

## Global Constraints

- **Target framework:** `net8.0-windows`
- **Platform:** Windows x64 only
- **Dependencies:** None beyond .NET 8 SDK
- **DPI:** PerMonitorV2 awareness via app.manifest
- **Settings path:** `%APPDATA%\smolscren\settings.json`
- **Namespace:** `smolscren`
- **Build verification:** `dotnet build` must show zero warnings
- **Publish:** `dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true`

---

## File Structure

```
D:\smolscren\
├── smolscren.csproj              — project file (WPF + WinForms)
├── app.manifest                  — DPI awareness
├── App.xaml                      — application entry, startup video path arg
├── App.xaml.cs
├── MainWindow.xaml               — borderless window + MediaElement
├── MainWindow.xaml.cs            — drag, resize, context menu, wiring
├── WindowStateManager.cs         — Win32 click-through lock/unlock
├── HotkeyManager.cs             — global hotkey Ctrl+Alt+Shift+V
├── TrayIcon.cs                   — NotifyIcon + context menu
├── Settings.cs                   — JSON persistence
└── docs\
```

---

### Task 1: Scaffold project

**Files:**
- Create: `smolscren.csproj`
- Create: `app.manifest`
- Create: `App.xaml`
- Create: `App.xaml.cs`

**Interfaces:**
- Produces: `App.StartupVideoPath` (string?, from command-line args)

- [ ] **Step 1: Create smolscren.csproj**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <UseWPF>true</UseWPF>
    <UseWindowsForms>true</UseWindowsForms>
    <ApplicationManifest>app.manifest</ApplicationManifest>
  </PropertyGroup>
</Project>
```

- [ ] **Step 2: Create app.manifest**

```xml
<?xml version="1.0" encoding="utf-8"?>
<assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm.v1">
  <application xmlns="urn:schemas-microsoft-com:asm.v3">
    <windowsSettings>
      <dpiAware xmlns="http://schemas.microsoft.com/SMI/2005/WindowsSettings">true</dpiAware>
      <dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">PerMonitorV2</dpiAwareness>
    </windowsSettings>
  </application>
</assembly>
```

- [ ] **Step 3: Create App.xaml**

```xml
<Application x:Class="smolscren.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             StartupUri="MainWindow.xaml">
</Application>
```

- [ ] **Step 4: Create App.xaml.cs**

```csharp
using System.Windows;

namespace smolscren;

public partial class App : Application
{
    public string? StartupVideoPath { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        if (e.Args.Length > 0)
            StartupVideoPath = e.Args[0];
        base.OnStartup(e);
    }
}
```

- [ ] **Step 5: Build verification**

Run: `dotnet build`
Expected: Build succeeds (no MainWindow yet — that's fine, it will fail. Just verify the project file parses)

Actually — since MainWindow.xaml doesn't exist yet, `dotnet build` will fail. Skip build verification for this task; it will pass once Task 6 creates MainWindow.

- [ ] **Step 6: Commit**

```bash
git init
git add smolscren.csproj app.manifest App.xaml App.xaml.cs
git commit -m "scaffold: project file, manifest, app entry"
```

---

### Task 2: Settings persistence

**Files:**
- Create: `Settings.cs`
- Create: `Settings.Tests/Settings.Tests.csproj`
- Create: `Settings.Tests/SettingsTests.cs`

**Test project file** (`Settings.Tests/Settings.Tests.csproj`):

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\smolscren.csproj" />
  </ItemGroup>
</Project>
```

**Interfaces:**
- Produces: `Settings` class with `Load()`, `Save()`, properties `VideoPath`, `Left`, `Top`, `Width`, `Height`, `KeepOnTop`

- [ ] **Step 1: Write the failing test**

`Settings.Tests/SettingsTests.cs`:

```csharp
using System;
using System.IO;
using Xunit;

public class SettingsTests : IDisposable
{
    private readonly string _testDir;

    public SettingsTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "smolscren_test_" + Guid.NewGuid());
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    [Fact]
    public void Load_ReturnsDefaults_WhenNoFileExists()
    {
        var settings = Settings.Load(_testDir);
        Assert.Equal("", settings.VideoPath);
        Assert.Equal(100, settings.Left);
        Assert.Equal(100, settings.Top);
        Assert.Equal(400, settings.Width);
        Assert.Equal(300, settings.Height);
        Assert.True(settings.KeepOnTop);
    }

    [Fact]
    public void Save_WritesJsonFile()
    {
        var settings = new Settings { VideoPath = @"C:\test\video.mp4", Left = 50 };
        settings.Save(_testDir);
        var path = Path.Combine(_testDir, "settings.json");
        Assert.True(File.Exists(path));
        var json = File.ReadAllText(path);
        Assert.Contains("video.mp4", json);
    }

    [Fact]
    public void Load_RoundTrips_SavedValues()
    {
        var settings = new Settings { VideoPath = @"C:\test\test.mp4", Left = 200, Top = 150, Width = 500, Height = 350, KeepOnTop = false };
        settings.Save(_testDir);
        var loaded = Settings.Load(_testDir);
        Assert.Equal(settings.VideoPath, loaded.VideoPath);
        Assert.Equal(settings.Left, loaded.Left);
        Assert.Equal(settings.Top, loaded.Top);
        Assert.Equal(settings.Width, loaded.Width);
        Assert.Equal(settings.Height, loaded.Height);
        Assert.Equal(settings.KeepOnTop, loaded.KeepOnTop);
    }

    [Fact]
    public void Load_ReturnsDefaults_WhenFileIsCorrupt()
    {
        var path = Path.Combine(_testDir, "settings.json");
        File.WriteText(path, "{ invalid json !!!");
        var settings = Settings.Load(_testDir);
        Assert.Equal("", settings.VideoPath);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Settings.Tests/`
Expected: FAIL — Settings class not found

- [ ] **Step 3: Write Settings.cs**

```csharp
using System;
using System.IO;
using System.Text.Json;

namespace smolscren;

public class Settings
{
    public string VideoPath { get; set; } = "";
    public double Left { get; set; } = 100;
    public double Top { get; set; } = 100;
    public double Width { get; set; } = 400;
    public double Height { get; set; } = 300;
    public bool KeepOnTop { get; set; } = true;

    public static Settings Load()
    {
        return Load(null);
    }

    public static Settings Load(string? overrideDir)
    {
        try
        {
            var dir = overrideDir ?? GetSettingsDir();
            var path = Path.Combine(dir, "settings.json");
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<Settings>(json) ?? new Settings();
            }
        }
        catch { /* corrupt or missing — start fresh */ }
        return new Settings();
    }

    public void Save()
    {
        Save(null);
    }

    public void Save(string? overrideDir)
    {
        var dir = overrideDir ?? GetSettingsDir();
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "settings.json");
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    private static string GetSettingsDir()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "smolscren");
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test Settings.Tests/`
Expected: 4 tests pass

- [ ] **Step 5: Commit**

```bash
git add Settings.cs Settings.Tests/
git commit -m "feat(settings): JSON persistence with round-trip and corruption handling"
```

---

### Task 3: WindowStateManager — click-through lock/unlock

**Files:**
- Create: `WindowStateManager.cs`

**Interfaces:**
- Consumes: `System.Windows.Window`
- Produces: `WindowStateManager` class with `IsLocked`, `ToggleLock()`, `Lock()`, `Unlock()`, `ApplyInitialState()`, event `LockStateChanged`

- [ ] **Step 1: Create WindowStateManager.cs**

```csharp
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace smolscren;

public class WindowStateManager
{
    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_TOOLWINDOW = 0x00000080;

    private readonly Window _window;
    private bool _isLocked = true;

    public bool IsLocked => _isLocked;

    public event EventHandler<bool>? LockStateChanged;

    public WindowStateManager(Window window)
    {
        _window = window;
    }

    public void ApplyInitialState()
    {
        if (_isLocked)
            MakeClickThrough();
    }

    public void ToggleLock()
    {
        if (_isLocked) Unlock();
        else Lock();
    }

    public void Lock()
    {
        _isLocked = true;
        MakeClickThrough();
        LockStateChanged?.Invoke(this, true);
    }

    public void Unlock()
    {
        _isLocked = false;
        RemoveClickThrough();
        LockStateChanged?.Invoke(this, false);
    }

    private void MakeClickThrough()
    {
        var hwnd = new WindowInteropHelper(_window).Handle;
        var exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW);
    }

    private void RemoveClickThrough()
    {
        var hwnd = new WindowInteropHelper(_window).Handle;
        var exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle & ~WS_EX_TRANSPARENT);
    }
}
```

- [ ] **Step 2: Build verification**

Run: `dotnet build`
Expected: Build succeeds (WindowStateManager compiles)

- [ ] **Step 3: Commit**

```bash
git add WindowStateManager.cs
git commit -m "feat(window-state): Win32 click-through lock/unlock manager"
```

---

### Task 4: HotkeyManager — global hotkey

**Files:**
- Create: `HotkeyManager.cs`

**Interfaces:**
- Consumes: `System.Windows.Window`, `Action` callback
- Produces: `HotkeyManager` class with `Register() -> bool`, `Dispose()`

- [ ] **Step 1: Create HotkeyManager.cs**

```csharp
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace smolscren;

public class HotkeyManager : IDisposable
{
    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private const int WM_HOTKEY = 0x0312;
    private const int HOTKEY_ID = 1;
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint VK_V = 0x56;

    private readonly IntPtr _hwnd;
    private readonly HwndSource? _source;
    private readonly Action _onHotkey;
    private bool _disposed;

    public HotkeyManager(Window window, Action onHotkey)
    {
        var helper = new WindowInteropHelper(window);
        _hwnd = helper.Handle;
        _onHotkey = onHotkey;
        _source = HwndSource.FromHwnd(_hwnd);
        _source?.AddHook(WndProc);
    }

    public bool Register()
    {
        return RegisterHotKey(_hwnd, HOTKEY_ID, MOD_ALT | MOD_CONTROL | MOD_SHIFT, VK_V);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
        {
            _onHotkey();
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        UnregisterHotKey(_hwnd, HOTKEY_ID);
        _source?.RemoveHook(WndProc);
        _source?.Dispose();
    }
}
```

- [ ] **Step 2: Build verification**

Run: `dotnet build`
Expected: Build succeeds

- [ ] **Step 3: Commit**

```bash
git add HotkeyManager.cs
git commit -m "feat(hotkey): global hotkey Ctrl+Alt+Shift+V via Win32 RegisterHotKey"
```

---

### Task 5: TrayIcon — system tray

**Files:**
- Create: `TrayIcon.cs`

**Interfaces:**
- Consumes: callbacks for unlock toggle, open video, exit, keep-on-top
- Produces: `TrayIcon` class with `UpdateState(bool isLocked, bool keepOnTop)`, `Dispose()`

- [ ] **Step 1: Create TrayIcon.cs**

```csharp
using System;
using System.Windows.Forms;

namespace smolscren;

public class TrayIcon : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripItem _unlockItem;
    private readonly ToolStripItem _topItem;
    private bool _disposed;

    public TrayIcon(
        Action onUnlockToggle,
        Action onOpenVideo,
        Action onExit,
        Func<bool> keepOnTop,
        Action<bool> onKeepOnTopChanged)
    {
        _notifyIcon = new NotifyIcon();

        var menu = new ContextMenuStrip();

        _unlockItem = new ToolStripButton("Unlock (move/resize)");
        _unlockItem.Click += (s, e) => onUnlockToggle();
        menu.Items.Add(_unlockItem);

        _topItem = new ToolStripButton("Keep on top");
        _topItem.Click += (s, e) => onKeepOnTopChanged(!keepOnTop());
        menu.Items.Add(_topItem);

        var openItem = new ToolStripButton("Open video...");
        openItem.Click += (s, e) => onOpenVideo();
        menu.Items.Add(openItem);

        menu.Items.Add(new ToolStripSeparator());

        var exitItem = new ToolStripButton("Exit");
        exitItem.Click += (s, e) => onExit();
        menu.Items.Add(exitItem);

        _notifyIcon.ContextMenuStrip = menu;
        _notifyIcon.DoubleClick += (s, e) => onUnlockToggle();
        _notifyIcon.Visible = true;
    }

    public void UpdateState(bool isLocked, bool keepOnTop)
    {
        _notifyIcon.Text = isLocked ? "smolscren — locked" : "smolscren — unlocked";
        if (_unlockItem is ToolStripButton unlockBtn)
            unlockBtn.Checked = !isLocked;
        if (_topItem is ToolStripButton topBtn)
            topBtn.Checked = keepOnTop;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _notifyIcon.Dispose();
    }
}
```

- [ ] **Step 2: Build verification**

Run: `dotnet build`
Expected: Build succeeds

- [ ] **Step 3: Commit**

```bash
git add TrayIcon.cs
git commit -m "feat(tray): system tray icon with unlock, keep-on-top, open, exit menu"
```

---

### Task 6: MainWindow — XAML + code-behind

**Files:**
- Create: `MainWindow.xaml`
- Create: `MainWindow.xaml.cs`

**Interfaces:**
- Consumes: `Settings`, `WindowStateManager`, `HotkeyManager`, `TrayIcon`
- Produces: working application

- [ ] **Step 1: Create MainWindow.xaml**

```xml
<Window x:Class="smolscren.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="smolscren"
        WindowStyle="None"
        AllowsTransparency="True"
        Topmost="True"
        ShowInTaskbar="False"
        Background="Transparent"
        Width="400" Height="300"
        Left="100" Top="100">
    <Grid>
        <MediaElement x:Name="VideoPlayer"
                      Stretch="UniformToFill"
                      LoadedBehavior="Manual"
                      UnloadedBehavior="Manual"
                      IsMuted="True"
                      MediaEnded="VideoPlayer_MediaEnded"
                      MediaFailed="VideoPlayer_MediaFailed"/>
        <Border x:Name="UnlockBorder"
                BorderBrush="#80FFFFFF"
                BorderThickness="2"
                CornerRadius="4"
                Visibility="Collapsed"
                IsHitTestVisible="False"/>
    </Grid>
</Window>
```

- [ ] **Step 2: Create MainWindow.xaml.cs**

```csharp
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;

namespace smolscren;

public partial class MainWindow : Window
{
    private Settings _settings = null!;
    private WindowStateManager _stateManager = null!;
    private HotkeyManager? _hotkey;
    private TrayIcon? _trayIcon;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _settings = Settings.Load();

        // Restore window geometry
        Left = _settings.Left;
        Top = _settings.Top;
        Width = _settings.Width;
        Height = _settings.Height;
        Topmost = _settings.KeepOnTop;

        // Resolve video: CLI arg → settings → file dialog
        string? videoPath = ResolveVideoPath();

        if (string.IsNullOrEmpty(videoPath))
        {
            Application.Current.Shutdown();
            return;
        }

        LoadVideo(videoPath);

        // Window state (click-through)
        _stateManager = new WindowStateManager(this);
        _stateManager.LockStateChanged += (_, locked) => UpdateVisualState();
        _stateManager.ApplyInitialState();

        // Global hotkey
        _hotkey = new HotkeyManager(this, () => _stateManager.ToggleLock());
        if (!_hotkey.Register())
        {
            MessageBox.Show(
                "Hotkey Ctrl+Alt+Shift+V is already in use by another app.\nUse the tray icon to unlock.",
                "smolscren",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        // System tray
        _trayIcon = new TrayIcon(
            onUnlockToggle: () => _stateManager.ToggleLock(),
            onOpenVideo: OpenVideo,
            onExit: () => Application.Current.Shutdown(),
            keepOnTop: () => Topmost,
            onKeepOnTopChanged: val => { Topmost = val; _settings.KeepOnTop = val; });

        UpdateVisualState();
    }

    private string? ResolveVideoPath()
    {
        var app = Application.Current as App;
        if (!string.IsNullOrEmpty(app?.StartupVideoPath) && File.Exists(app.StartupVideoPath))
            return app.StartupVideoPath;

        if (!string.IsNullOrEmpty(_settings.VideoPath) && File.Exists(_settings.VideoPath))
            return _settings.VideoPath;

        var dlg = new OpenFileDialog
        {
            Title = "Select a video",
            Filter = "Video files|*.mp4;*.mkv;*.avi;*.mov;*.wmv;*.webm|All files|*.*"
        };

        if (dlg.ShowDialog(this) == true)
            return dlg.FileName;

        return null;
    }

    private void LoadVideo(string path)
    {
        try
        {
            VideoPlayer.Source = new Uri(path);
            VideoPlayer.Play();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Cannot load video:\n{ex.Message}", "smolscren",
                MessageBoxButton.OK, MessageBoxImage.Error);
            OpenVideo();
        }
    }

    private void VideoPlayer_MediaEnded(object sender, RoutedEventArgs e)
    {
        VideoPlayer.Position = TimeSpan.Zero;
        VideoPlayer.Play();
    }

    private void VideoPlayer_MediaFailed(object sender, ExceptionRoutedEventArgs e)
    {
        MessageBox.Show(
            $"Playback failed:\n{e.Exception.Message}",
            "smolscren",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        OpenVideo();
    }

    private void OpenVideo()
    {
        var dlg = new OpenFileDialog
        {
            Title = "Select a video",
            Filter = "Video files|*.mp4;*.mkv;*.avi;*.mov;*.wmv;*.webm|All files|*.*"
        };

        if (dlg.ShowDialog(this) == true)
        {
            _settings.VideoPath = dlg.FileName;
            LoadVideo(dlg.FileName);
        }
    }

    private void UpdateVisualState()
    {
        UnlockBorder.Visibility = _stateManager.IsLocked ? Visibility.Collapsed : Visibility.Visible;
        _trayIcon?.UpdateState(_stateManager.IsLocked, Topmost);
    }

    private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        _settings.Left = Left;
        _settings.Top = Top;
        _settings.Width = Width;
        _settings.Height = Height;
        _settings.Save();
        _hotkey?.Dispose();
        _trayIcon?.Dispose();
    }

    // Drag when unlocked
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        if (!_stateManager.IsLocked)
        {
            DragMove();
        }
        base.OnMouseLeftButtonDown(e);
    }

    // Right-click context menu when unlocked
    protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
    {
        if (!_stateManager.IsLocked)
        {
            var menu = new ContextMenu();
            var lockItem = new MenuItem { Header = "Lock" };
            lockItem.Click += (_, _) => _stateManager.Lock();
            menu.Items.Add(lockItem);

            var exitItem = new MenuItem { Header = "Exit" };
            exitItem.Click += (_, _) => Application.Current.Shutdown();
            menu.Items.Add(exitItem);

            ContextMenu = menu;
            ContextMenu.IsOpen = true;
        }
        base.OnMouseRightButtonUp(e);
    }
}
```

- [ ] **Step 3: Build verification**

Run: `dotnet build`
Expected: Build succeeds, zero warnings

- [ ] **Step 4: Commit**

```bash
git add MainWindow.xaml MainWindow.xaml.cs
git commit -m "feat(main-window): video player, drag, resize, context menu, wiring"
```

---

### Task 7: Build + publish verification

**Files:** none

- [ ] **Step 1: Clean build**

Run: `dotnet build`
Expected: Build succeeded, 0 Warning(s), 0 Error(s)

- [ ] **Step 2: Publish self-contained exe**

Run: `dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true`
Expected: `bin/Release/net8.0-windows/win-x64/publish/smolscren.exe` exists

- [ ] **Step 3: Commit any fixes**

If build fixed anything:
```bash
git add -A
git commit -m "fix: build cleanup"
```

---

### Task 8: Manual test pass

**Files:** none

Run the published exe and walk the test matrix from the spec (§6.1). Check off each:

- [ ] 1. Launch with no prior settings → OpenFileDialog appears
- [ ] 2. Select video → plays, loops, no audio
- [ ] 3. Click through window → reaches desktop underneath
- [ ] 4. Press Ctrl+Alt+Shift+V → unlocks (border visible)
- [ ] 5. Drag unlocked window → moves
- [ ] 6. Resize via corner grip → resizes
- [ ] 7. Right-click unlocked → context menu with Lock and Exit
- [ ] 8. Click Exit → closes, tray icon gone
- [ ] 9. Tray right-click → Exit → closes
- [ ] 10. Relaunch → same video, position restored
- [ ] 11. Delete video file while playing → graceful error
- [ ] 12. Launch with CLI path arg → that video loads
- [ ] 13. Tray → Open video... → new video loads and is remembered
- [ ] 14. Tray → Keep on top off → other windows cover video
- [ ] 15. Hotkey already used → warning, tray still works
- [ ] 16. Drag between monitors → no blur or jump
- [ ] 17. Play 1080p for several minutes → smooth, no flicker

- [ ] **All tests pass → done**
