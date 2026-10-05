using System;
using System.Threading;
using System.Windows;

namespace VWP;
public partial class App : System.Windows.Application
{
    private Mutex? mutex;
    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += (_, args) => {
            System.IO.File.WriteAllText(System.IO.Path.Combine(AppContext.BaseDirectory,"startup-error.log"),args.Exception.ToString());
            args.Handled=true; Shutdown(1);
        };
        mutex = new Mutex(true, "Local\\DomovoyVWP", out bool first);
        if (!first) { MessageBox.Show("VWP уже запущен. Откройте приложение через трей."); Shutdown(); return; }
        base.OnStartup(e);
    }
    protected override void OnExit(ExitEventArgs e) { mutex?.Dispose(); base.OnExit(e); }
}
