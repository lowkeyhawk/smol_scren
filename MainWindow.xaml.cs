using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
