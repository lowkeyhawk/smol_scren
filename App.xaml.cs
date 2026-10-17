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
