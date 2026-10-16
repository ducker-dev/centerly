using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Centerly.App.Resources;
using H.NotifyIcon;
using H.NotifyIcon.Core;

namespace Centerly.App.Services;

/// <summary>Notification area icon with the app menu.</summary>
internal sealed class AppTrayIcon : IDisposable
{
    private readonly TaskbarIcon _icon;
    private readonly MenuItem _pauseItem;
    private readonly CenteringService _centering;
    private readonly SettingsService _settings;

    public AppTrayIcon(CenteringService centering, SettingsService settings, Action openWindow, Action exit)
    {
        _centering = centering;
        _settings = settings;

        var openItem = new MenuItem { Header = Strings.TrayOpen, FontWeight = FontWeights.SemiBold };
        openItem.Click += (_, _) => openWindow();

        _pauseItem = new MenuItem { Header = Strings.TrayPause, IsCheckable = true };
        _pauseItem.Click += (_, _) => _centering.IsPaused = _pauseItem.IsChecked;

        var exitItem = new MenuItem { Header = Strings.TrayExit };
        exitItem.Click += (_, _) => exit();

        _icon = new TaskbarIcon
        {
            IconSource = new BitmapImage(new Uri("pack://application:,,,/Assets/app.ico")),
            ContextMenu = new ContextMenu { Items = { openItem, _pauseItem, new Separator(), exitItem } },
            MenuActivation = PopupActivationMode.RightClick,
            NoLeftClickDelay = true,
        };
        _icon.TrayLeftMouseUp += (_, _) => openWindow();

        _centering.StateChanged += (_, _) => Refresh();
        _settings.Changed += (_, _) => Refresh();
        Refresh();

        // Efficiency mode (EcoQoS) would throttle us while hidden and delay hotkey reactions.
        _icon.ForceCreate(enablesEfficiencyMode: false);
    }

    public void ShowNotification(string title, string message, NotificationIcon icon = NotificationIcon.Info) =>
        _icon.ShowNotification(title, message, icon);

    public void Dispose() => _icon.Dispose();

    private void Refresh()
    {
        _pauseItem.IsChecked = _centering.IsPaused;

        var hotkey = _settings.Current.CenterHotkey;
        _icon.ToolTipText = _centering.IsPaused ? Strings.TrayTooltipPaused
            : hotkey.IsNone ? "Centerly"
            : $"Centerly — {hotkey.ToDisplayString()}";
    }
}
