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
