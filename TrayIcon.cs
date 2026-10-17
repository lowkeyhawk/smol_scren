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
