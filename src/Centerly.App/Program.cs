using Centerly.App.Services;
using Velopack;

namespace Centerly.App;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        // Must run first: handles install/update/uninstall hooks and exits the process when invoked by the installer.
        VelopackApp.Build()
            .OnBeforeUninstallFastCallback(_ => Autostart.RemoveForUninstall())
            .Run();

        // Short-lived elevated helper invocations (e.g. creating the admin autostart task) never start the UI.
        if (ElevatedCommands.TryRun(args, out var exitCode))
            return exitCode;

        var app = new App();
        app.InitializeComponent();
        return app.Run();
    }
}
