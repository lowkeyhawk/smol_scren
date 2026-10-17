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
