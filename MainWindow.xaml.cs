using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Controls.Primitives;
using Microsoft.Win32;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

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

    private void VideoPlayer_MediaFailed(object sender, System.Windows.ExceptionRoutedEventArgs e)
    {
        MessageBox.Show(
            $"Playback failed:\n{e.ErrorException.Message}",
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
        GripLayer.Visibility = _stateManager.IsLocked ? Visibility.Collapsed : Visibility.Visible;
        _trayIcon?.UpdateState(_stateManager.IsLocked, Topmost);
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
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

    // Corner-grip resize — only reachable while unlocked (GripLayer is collapsed when locked).
    // WindowStyle=None forces ResizeMode=NoResize, so these grips are the sole resize path.
    //
    // DragDelta is deliberately NOT used for the geometry: Thumb.DragDeltaEventArgs is
    // measured relative to the Thumb itself, and the Thumb rides along with the window as
    // it grows. Each delta therefore arrives as (cursorDelta - windowGrowth), which solves
    // to exactly half the cursor travel — a 120px drag only grew the window 62px. Reading
    // Mouse.GetPosition(null) gives the cursor's absolute screen position instead, which
    // has no such feedback. Both are in device-independent pixels, so they compose.
    private double _dragLeft, _dragTop, _dragWidth, _dragHeight;
    private System.Windows.Point _dragStartMouse;

    // Absolute screen position of the cursor, in device-independent pixels.
    // Mouse.GetPosition(null) is relative to this window's own client origin, so the
    // window's Left/Top have to be added back. Working in absolute screen space is what
    // keeps the top-left grips from feeding the window's own movement back into itself.
    private System.Windows.Point CursorScreen()
    {
        var m = System.Windows.Input.Mouse.GetPosition(null);
        return new System.Windows.Point(m.X + Left, m.Y + Top);
    }

    private const double GripMinWidth = 160;
    private const double GripMinHeight = 120;

    private void Grip_DragStarted(object sender, DragStartedEventArgs e)
    {
        _dragLeft = Left;
        _dragTop = Top;
        _dragWidth = Width;
        _dragHeight = Height;
        _dragStartMouse = CursorScreen();
    }

    private void GripTopLeft_DragDelta(object sender, DragDeltaEventArgs e)
    {
        var m = CursorScreen();
        var w = Math.Max(GripMinWidth, _dragWidth - (m.X - _dragStartMouse.X));
        var h = Math.Max(GripMinHeight, _dragHeight - (m.Y - _dragStartMouse.Y));
        Left = _dragLeft + (_dragWidth - w);
        Top = _dragTop + (_dragHeight - h);
        Width = w;
        Height = h;
    }

    private void GripTopRight_DragDelta(object sender, DragDeltaEventArgs e)
    {
        var m = CursorScreen();
        var h = Math.Max(GripMinHeight, _dragHeight - (m.Y - _dragStartMouse.Y));
        Top = _dragTop + (_dragHeight - h);
        Width = Math.Max(GripMinWidth, _dragWidth + (m.X - _dragStartMouse.X));
        Height = h;
    }

    private void GripBottomLeft_DragDelta(object sender, DragDeltaEventArgs e)
    {
        var m = CursorScreen();
        var w = Math.Max(GripMinWidth, _dragWidth - (m.X - _dragStartMouse.X));
        Left = _dragLeft + (_dragWidth - w);
        Width = w;
        Height = Math.Max(GripMinHeight, _dragHeight + (m.Y - _dragStartMouse.Y));
    }

    private void GripBottomRight_DragDelta(object sender, DragDeltaEventArgs e)
    {
        var m = CursorScreen();
        Width = Math.Max(GripMinWidth, _dragWidth + (m.X - _dragStartMouse.X));
        Height = Math.Max(GripMinHeight, _dragHeight + (m.Y - _dragStartMouse.Y));
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
