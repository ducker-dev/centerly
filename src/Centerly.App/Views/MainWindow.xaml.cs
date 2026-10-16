using System.ComponentModel;
using Centerly.App.Controls;
using Centerly.App.Resources;
using Centerly.App.Services;
using Centerly.App.ViewModels;
using Centerly.Core.AutoCenter;
using Microsoft.Win32;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace Centerly.App.Views;

internal sealed partial class MainWindow : FluentWindow, IAppPicker
{
    public MainWindow(MainViewModel viewModel)
    {
        DataContext = viewModel;
        viewModel.Picker = this;
        InitializeComponent();
        SystemThemeWatcher.Watch(this);

        WindowPickerBehavior.Attach(
            CrosshairButton,
            viewModel.AddPickedWindow,
            viewModel.ShowCrosshairHint,
            picked => $"{AppIcons.GetDisplayName(picked.App.ExecutablePath, picked.App.ProcessName)} ({picked.App.ProcessName})");
    }

    /// <summary>When false, closing the window only hides it to the tray.</summary>
    public bool AllowClose { get; set; }

    /// <summary>Raised when the window is hidden to the tray instead of closing.</summary>
    public event EventHandler? HiddenToTray;

    public void ShowAndActivate()
    {
        Show();
        if (WindowState == System.Windows.WindowState.Minimized)
            WindowState = System.Windows.WindowState.Normal;
        Activate();
    }

    public async Task<RunningApp?> PickRunningAppAsync(IEnumerable<string> excludedProcessNames)
    {
        var pickerViewModel = new RunningAppsPickerViewModel(RunningApps.GetWithWindows(), excludedProcessNames);
        var picker = new RunningAppsPicker(pickerViewModel);
        var dialog = new ContentDialog(DialogHost)
        {
            Title = Strings.PickerTitle,
            Content = picker,
            PrimaryButtonText = Strings.PickerAdd,
            CloseButtonText = Strings.Cancel,
            DefaultButton = ContentDialogButton.Primary,
        };
        picker.ItemActivated += (_, _) => dialog.Hide(ContentDialogResult.Primary);

        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary ? pickerViewModel.Selected?.App : null;
    }

    public string? PickExecutable()
    {
        var dialog = new OpenFileDialog
        {
            Title = Strings.ExeDialogTitle,
            Filter = Strings.ExeDialogFilter,
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        };
        return dialog.ShowDialog(this) == true ? dialog.FileName : null;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!AllowClose)
        {
            e.Cancel = true;
            Hide();
            HiddenToTray?.Invoke(this, EventArgs.Empty);
        }

        base.OnClosing(e);
    }
}
