using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Centerly.App.Resources;
using Centerly.App.Services;
using Centerly.Core.AutoCenter;
using Centerly.Core.Centering;
using Centerly.Core.Hotkeys;
using Centerly.Core.Settings;
using Wpf.Ui.Controls;

namespace Centerly.App.ViewModels;

internal sealed record Option<T>(T Value, string Title);

internal sealed partial class MainViewModel : ObservableObject
{
    private readonly CenteringService _centering;
    private readonly SettingsService _settings;
    private readonly UpdateService _updates;
    private readonly IAppLifetime _lifetime;
    private readonly AppLanguage _startupLanguage;
    private AutostartMode _autostartMode;

    [ObservableProperty]
    public partial bool IsMessageOpen { get; set; }

    [ObservableProperty]
    public partial string MessageTitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string MessageText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial InfoBarSeverity MessageSeverity { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SetAutostartCommand))]
    [NotifyPropertyChangedFor(nameof(IsAutostartEditable))]
    public partial bool IsAutostartBusy { get; set; }

    public bool IsAutostartEditable => !IsAutostartBusy;

    [ObservableProperty]
    public partial string UpdateStatus { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CheckForUpdatesCommand), nameof(InstallUpdateCommand))]
    public partial bool IsUpdateBusy { get; set; }

    public MainViewModel(CenteringService centering, SettingsService settings, UpdateService updates, IAppLifetime lifetime)
    {
        _centering = centering;
        _settings = settings;
        _updates = updates;
        _lifetime = lifetime;
        _startupLanguage = settings.Current.Language;

        OversizeOptions =
        [
            new(OversizeBehavior.KeepSize, Strings.OversizeKeepSize),
            new(OversizeBehavior.ShrinkToFit, Strings.OversizeShrink),
        ];
        AutostartOptions =
        [
            new(AutostartMode.Off, Strings.AutostartOff),
            new(AutostartMode.Normal, Strings.AutostartNormal),
            new(AutostartMode.Elevated, Strings.AutostartElevated),
        ];
        AutoCenterModeOptions =
        [
            new(AutoCenterMode.ListedApps, Strings.AutoModeListed),
            new(AutoCenterMode.AllApps, Strings.AutoModeAll),
        ];
        LanguageOptions =
        [
            new(AppLanguage.System, Strings.LanguageSystem),
            new(AppLanguage.Russian, "Русский"),
            new(AppLanguage.English, "English"),
        ];

        _autostartMode = Autostart.GetMode();

        _centering.StateChanged += (_, _) => OnCenteringStateChanged();
        OnCenteringStateChanged();

        _updates.AvailableChanged += (_, _) => OnUpdateAvailableChanged();
        OnUpdateAvailableChanged();

        Rules = new ObservableCollection<RuleItemViewModel>(CurrentListRules().Select(CreateRuleItem));
        Rules.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasRules));
    }

    /// <summary>Set by the window: shows the dialogs for adding an application.</summary>
    public IAppPicker? Picker { get; set; }

    public string VersionText => string.Format(CultureInfo.CurrentCulture, Strings.VersionFormat, AppPaths.Version);

    // Hotkeys

    public Hotkey CenterHotkey
    {
        get => _centering.GetHotkey(HotkeyAction.CenterActiveWindow);
        set
        {
            ApplyHotkey(HotkeyAction.CenterActiveWindow, value);
            // Re-read even on rejection so the box shows the hotkey that is actually in effect.
            OnPropertyChanged();
        }
    }

    public Hotkey NextMonitorHotkey
    {
        get => _centering.GetHotkey(HotkeyAction.MoveToNextMonitor);
        set
        {
            ApplyHotkey(HotkeyAction.MoveToNextMonitor, value);
            OnPropertyChanged();
        }
    }

    public Hotkey PreviousMonitorHotkey
    {
        get => _centering.GetHotkey(HotkeyAction.MoveToPreviousMonitor);
        set
        {
            ApplyHotkey(HotkeyAction.MoveToPreviousMonitor, value);
            OnPropertyChanged();
        }
    }

    public Hotkey UndoHotkey
    {
        get => _centering.GetHotkey(HotkeyAction.Undo);
        set
        {
            ApplyHotkey(HotkeyAction.Undo, value);
            OnPropertyChanged();
        }
    }

    public bool IsRecordingHotkey
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
                _centering.SetSuspended(value);
        }
    }

    public bool IsEnabled
    {
        get => !_centering.IsPaused;
        set => _centering.IsPaused = !value;
    }

    // Rules

    public IReadOnlyList<Option<AutoCenterMode>> AutoCenterModeOptions { get; }

    public Option<AutoCenterMode> SelectedAutoCenterMode
    {
        get => AutoCenterModeOptions.First(o => o.Value == _settings.Current.AutoCenterMode);
        set
        {
            if (value is null || value.Value == _settings.Current.AutoCenterMode) return;
            _settings.Update(s => s with { AutoCenterMode = value.Value });

            // Each mode has its own list; the other one is kept for when the user switches back.
            Rules.Clear();
            foreach (var rule in CurrentListRules())
                Rules.Add(CreateRuleItem(rule));
            IsMessageOpen = false;
            OnPropertyChanged();
            OnPropertyChanged(nameof(RulesEmptyText));
        }
    }

    /// <summary>The list of the current mode: apps to center, or exclusions when all apps are centered.</summary>
    public ObservableCollection<RuleItemViewModel> Rules { get; }

    public bool HasRules => Rules.Count > 0;

    public string RulesEmptyText => _settings.Current.AutoCenterMode == AutoCenterMode.AllApps
        ? Strings.RulesEmptyAllApps
        : Strings.RulesEmpty;

    // General

    public IReadOnlyList<Option<OversizeBehavior>> OversizeOptions { get; }

    public Option<OversizeBehavior> SelectedOversize
    {
        get => OversizeOptions.First(o => o.Value == _settings.Current.Oversize);
        set
        {
            if (value is null) return;
            _settings.Update(s => s with { Oversize = value.Value });
            OnPropertyChanged();
        }
    }

    public IReadOnlyList<Option<AutostartMode>> AutostartOptions { get; }

    public Option<AutostartMode> SelectedAutostart
    {
        get => AutostartOptions.First(o => o.Value == _autostartMode);
        set
        {
            if (value is not null && value.Value != _autostartMode)
                SetAutostartCommand.Execute(value.Value);
        }
    }

    public bool IsElevated { get; } = Autostart.IsProcessElevated;

    public IReadOnlyList<Option<AppLanguage>> LanguageOptions { get; }

    public Option<AppLanguage> SelectedLanguage
    {
        get => LanguageOptions.First(o => o.Value == _settings.Current.Language);
        set
        {
            if (value is null) return;
            _settings.Update(s => s with { Language = value.Value });
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsRestartRequired));
        }
    }

    public bool IsRestartRequired => _settings.Current.Language != _startupLanguage;

    // Updates

    public bool AreUpdatesSupported => _updates.IsSupported;

    public bool IsUpdateAvailable => _updates.Available is not null;

    public bool CheckForUpdatesAutomatically
    {
        get => _settings.Current.CheckForUpdates;
        set
        {
            _settings.Update(s => s with { CheckForUpdates = value });
            OnPropertyChanged();
        }
    }

    [RelayCommand]
    private async Task AddRunningAppAsync()
    {
        if (Picker is null) return;
        var app = await Picker.PickRunningAppAsync(Rules.Select(r => r.ProcessName));
        if (app is not null)
            AddRule(app.ProcessName, app.ExecutablePath);
    }

    [RelayCommand]
    private void AddExecutable()
    {
        if (Picker?.PickExecutable() is { } path)
            AddRule(Path.GetFileName(path), path);
    }

    /// <summary>Called by the crosshair when the user drops it onto a window.</summary>
    public void AddPickedWindow(PickedWindow picked) => AddRule(picked.App.ProcessName, picked.App.ExecutablePath);

    public void ShowCrosshairHint() =>
        ShowMessage(InfoBarSeverity.Informational, Strings.MsgCrosshairHintTitle, Strings.MsgCrosshairHintText);

    [RelayCommand(CanExecute = nameof(CanSetAutostart))]
    private async Task SetAutostartAsync(AutostartMode mode)
    {
        IsAutostartBusy = true;
        try
        {
            // May wait for a UAC prompt; keep the UI responsive.
            var succeeded = await Task.Run(() => Autostart.SetMode(mode));
            if (!succeeded)
                ShowMessage(InfoBarSeverity.Warning, Strings.MsgAutostartFailedTitle, Strings.MsgAutostartFailedText);
        }
        finally
        {
            _autostartMode = Autostart.GetMode();
            IsAutostartBusy = false;
            OnPropertyChanged(nameof(SelectedAutostart));
        }
    }

    private bool CanSetAutostart() => !IsAutostartBusy;

    [RelayCommand]
    private void RestartElevated() => _lifetime.RestartElevated();

    [RelayCommand]
    private void Restart() => _lifetime.Restart();

    [RelayCommand(CanExecute = nameof(CanUseUpdates))]
    private async Task CheckForUpdatesAsync()
    {
        IsUpdateBusy = true;
        UpdateStatus = Strings.UpdatesChecking;
        try
        {
            var update = await _updates.CheckAsync();
            if (update is null)
                UpdateStatus = _updates.Available is null ? Strings.UpdatesUpToDate : UpdateStatus;
            OnUpdateAvailableChanged();
        }
        finally
        {
            IsUpdateBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanUseUpdates))]
    private async Task InstallUpdateAsync()
    {
        IsUpdateBusy = true;
        try
        {
            var installed = await _updates.InstallAndRestartAsync(percent =>
                UpdateStatus = string.Format(CultureInfo.CurrentCulture, Strings.UpdatesInstallingFormat, percent));
            if (!installed)
                UpdateStatus = Strings.UpdatesInstallFailed;
        }
        finally
        {
            IsUpdateBusy = false;
        }
    }

    private bool CanUseUpdates() => !IsUpdateBusy;

    [RelayCommand]
    private static void OpenSettingsFolder() => OpenFolder(Path.GetDirectoryName(AppPaths.SettingsFile)!);

    [RelayCommand]
    private static void OpenLogsFolder() => OpenFolder(AppPaths.LogsDirectory);

    private void AddRule(string processName, string executablePath)
    {
        if (Rules.FirstOrDefault(r => r.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase)) is { } existing)
        {
            existing.IsEnabled = true;
            ShowMessage(InfoBarSeverity.Informational, Strings.MsgAlreadyInListTitle, Format(Strings.MsgAlreadyInListText, existing.Title));
            return;
        }

        var rule = new AppRule
        {
            ProcessName = processName,
            DisplayName = AppIcons.GetDisplayName(executablePath, processName),
            ExecutablePath = executablePath,
            Action = CurrentListAction,
        };
        _settings.Update(s => s with { Rules = [.. s.Rules, rule] });
        Rules.Add(CreateRuleItem(rule));
        IsMessageOpen = false;
    }

    private RuleAction CurrentListAction =>
        _settings.Current.AutoCenterMode == AutoCenterMode.AllApps ? RuleAction.Ignore : RuleAction.Center;

    private IEnumerable<AppRule> CurrentListRules() => _settings.Current.Rules.Where(r => r.Action == CurrentListAction);

    private RuleItemViewModel CreateRuleItem(AppRule rule) => new(rule, UpdateRule, RemoveRule);

    private void UpdateRule(AppRule updated) =>
        _settings.Update(s => s with { Rules = [.. s.Rules.Select(r => r.Id == updated.Id ? updated : r)] });

    private void RemoveRule(RuleItemViewModel item)
    {
        _settings.Update(s => s with { Rules = [.. s.Rules.Where(r => r.Id != item.Rule.Id)] });
        Rules.Remove(item);
    }

    private void ApplyHotkey(HotkeyAction action, Hotkey hotkey)
    {
        var display = hotkey.ToDisplayString();
        if (!hotkey.IsNone && !hotkey.IsAcceptable)
        {
            ShowMessage(InfoBarSeverity.Warning, Strings.MsgHotkeyUnsuitableTitle, Format(Strings.MsgHotkeyUnsuitableText, display));
            return;
        }

        switch (_centering.ChangeHotkey(action, hotkey))
        {
            case HotkeyChangeResult.Applied:
                IsMessageOpen = false;
                break;
            case HotkeyChangeResult.InUseElsewhere:
                ShowMessage(InfoBarSeverity.Warning, Strings.MsgHotkeyInUseTitle, Format(Strings.MsgHotkeyInUseText, display));
                break;
            case HotkeyChangeResult.InUseByOtherAction:
                ShowMessage(InfoBarSeverity.Warning, Strings.MsgHotkeyInUseTitle, Format(Strings.MsgHotkeyDuplicateText, display));
                break;
            default:
                ShowMessage(InfoBarSeverity.Error, Strings.MsgHotkeyFailedTitle, Format(Strings.MsgHotkeyFailedText, display));
                break;
        }
    }

    private void OnCenteringStateChanged()
    {
        OnPropertyChanged(nameof(IsEnabled));

        // Registration can fail at startup or after resuming if another app grabbed the combination meanwhile.
        foreach (var action in Enum.GetValues<HotkeyAction>())
        {
            if (_centering.GetStatus(action) != HotkeyRegistrationStatus.Registered)
            {
                ShowMessage(InfoBarSeverity.Warning, Strings.MsgHotkeyNotWorkingTitle,
                    Format(Strings.MsgHotkeyNotWorkingText, _centering.GetHotkey(action).ToDisplayString()));
                return;
            }
        }
    }

    private void OnUpdateAvailableChanged()
    {
        OnPropertyChanged(nameof(IsUpdateAvailable));
        if (_updates.Available is { } update)
            UpdateStatus = Format(Strings.UpdatesAvailableFormat, update.TargetFullRelease.Version);
    }

    private void ShowMessage(InfoBarSeverity severity, string title, string text)
    {
        MessageSeverity = severity;
        MessageTitle = title;
        MessageText = text;
        IsMessageOpen = true;
    }

    private static string Format(string format, object? argument) =>
        string.Format(CultureInfo.CurrentCulture, format, argument);

    private static void OpenFolder(string path)
    {
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true })?.Dispose();
    }
}
