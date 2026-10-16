using System.Globalization;
using System.Windows;
using Centerly.App.Resources;
using Centerly.App.Services;
using Centerly.App.ViewModels;
using Centerly.App.Views;
using Centerly.Core.Centering;
using Centerly.Core.Hotkeys;
using Centerly.Core.Settings;
using H.NotifyIcon.Core;
using Serilog;
using Wpf.Ui.Appearance;

namespace Centerly.App;

internal sealed partial class App : Application, IAppLifetime
{
    private SingleInstance? _singleInstance;
    private SettingsService? _settings;
    private CenteringService? _centering;
    private UpdateService? _updates;
    private AppTrayIcon? _tray;
    private MainWindow? _window;
    private bool _trayHintShown;
    private bool _elevationHintShown;
    private string? _notifiedUpdateVersion;

    public void Restart()
    {
        if (Autostart.TryStartNewInstance(elevated: false))
            ExitApplication();
    }

    public void RestartElevated()
    {
        if (Autostart.TryStartNewInstance(elevated: true))
            ExitApplication();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // A replacement instance (restart, restart as administrator) waits for the old one to exit.
        var waitForPrevious = e.Args.Contains(ElevatedCommands.WaitForPreviousInstance)
            ? TimeSpan.FromSeconds(10)
            : TimeSpan.Zero;
        _singleInstance = SingleInstance.TryAcquire(waitForPrevious);
        if (_singleInstance is null)
        {
            Shutdown();
            return;
        }

        ConfigureLogging();
        RegisterCrashHandlers();
        Log.Information("Centerly {Version} starting on {OS}, elevated: {Elevated}",
            AppPaths.Version, Environment.OSVersion, Autostart.IsProcessElevated);

        try
        {
            Initialize(e.Args.Contains(Autostart.BackgroundArgument, StringComparer.OrdinalIgnoreCase));
        }
        catch (Exception ex)
        {
            // A half-initialized tray app is worse than a clear failure.
            Log.Fatal(ex, "Startup failed");
            MessageBox.Show(Format(Strings.ErrorStartup, ex.Message, AppPaths.LogsDirectory),
                "Centerly", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("Centerly exiting");
        _tray?.Dispose();
        _updates?.Dispose();
        _centering?.Dispose();
        _singleInstance?.Dispose();
        Log.CloseAndFlush();
        base.OnExit(e);
    }

    private void Initialize(bool startInBackground)
    {
        _settings = new SettingsService(new SettingsStore(AppPaths.SettingsFile));
        // Before any window or tray menu is created: resources are read once when they are built.
        UiLanguage.Apply(_settings.Current.Language);
        ApplicationThemeManager.ApplySystemTheme();

        _centering = new CenteringService(_settings);
        _centering.Centered += OnCentered;

        _updates = new UpdateService();
        _updates.AvailableChanged += (_, _) => OnUpdateAvailable();

        _window = new MainWindow(new MainViewModel(_centering, _settings, _updates, this));
        _window.HiddenToTray += OnHiddenToTray;

        _tray = new AppTrayIcon(_centering, _settings, ShowMainWindow, ExitApplication);
        _centering.Start();
        _updates.StartBackgroundChecks(() => _settings.Current.CheckForUpdates);

        _singleInstance!.ListenForActivation(() => Dispatcher.BeginInvoke(ShowMainWindow));

        if (!startInBackground || _settings.IsFirstRun)
            ShowMainWindow();
        else if (Enum.GetValues<HotkeyAction>().Where(a => _centering.GetStatus(a) != HotkeyRegistrationStatus.Registered).ToList() is [var failed, ..])
            _tray.ShowNotification(Strings.MsgHotkeyNotWorkingTitle,
                Format(Strings.NotifyHotkeyText, _centering.GetHotkey(failed).ToDisplayString()), NotificationIcon.Warning);

        if (_settings.LoadResult.Status == SettingsLoadStatus.Corrupted)
            _tray.ShowNotification(Strings.NotifySettingsResetTitle, Strings.NotifySettingsResetText, NotificationIcon.Warning);
    }

    private void ShowMainWindow() => _window?.ShowAndActivate();

    private void ExitApplication()
    {
        if (_window is not null)
        {
            _window.AllowClose = true;
            _window.Close();
        }

        Shutdown();
    }

    private void OnHiddenToTray(object? sender, EventArgs e)
    {
        if (_trayHintShown) return;
        _trayHintShown = true;
        _tray?.ShowNotification(Strings.NotifyTrayTitle, Strings.NotifyTrayText);
    }

    private void OnUpdateAvailable()
    {
        // Only nag from the tray once per version, and only when the window is not already showing it.
        if (_updates?.Available is not { } update || _window?.IsVisible == true)
            return;

        var version = update.TargetFullRelease.Version.ToString();
        if (version == _notifiedUpdateVersion)
            return;

        _notifiedUpdateVersion = version;
        _tray?.ShowNotification(Strings.NotifyUpdateTitle, Format(Strings.NotifyUpdateText, version));
    }

    private void OnCentered(object? sender, CenterResult result)
    {
        // Silent on success; only explain failures the user can do something about.
        if (result.Outcome != CenterOutcome.AccessDenied)
            return;

        Dispatcher.BeginInvoke(() =>
        {
            if (_elevationHintShown) return;
            _elevationHintShown = true;
            _tray?.ShowNotification(Strings.NotifyElevatedTitle, Strings.NotifyElevatedText, NotificationIcon.Warning);
        });
    }

    private static string Format(string format, params object?[] arguments) =>
        string.Format(CultureInfo.CurrentCulture, format, arguments);

    private static void ConfigureLogging()
    {
        Log.Logger = new LoggerConfiguration()
#if DEBUG
            .MinimumLevel.Debug()
#else
            .MinimumLevel.Information()
#endif
            .WriteTo.File(
                Path.Combine(AppPaths.LogsDirectory, "log-.txt"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                fileSizeLimitBytes: 5 * 1024 * 1024,
                rollOnFileSizeLimit: true,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }

    private void RegisterCrashHandlers()
    {
        DispatcherUnhandledException += (_, e) =>
        {
            Log.Error(e.Exception, "Unhandled UI exception");
            // A tray utility should survive a broken UI action rather than vanish silently.
            e.Handled = true;
            MessageBox.Show(Format(Strings.ErrorUnexpected, e.Exception.Message, AppPaths.LogsDirectory),
                "Centerly", MessageBoxButton.OK, MessageBoxImage.Error);
        };

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            Log.Fatal(e.ExceptionObject as Exception, "Unhandled exception, terminating: {Terminating}", e.IsTerminating);
            Log.CloseAndFlush();
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Log.Error(e.Exception, "Unobserved task exception");
            e.SetObserved();
        };
    }
}
