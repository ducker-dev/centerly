using System.ComponentModel;
using Centerly.Core.AutoCenter;
using Centerly.Core.Centering;
using Centerly.Core.Hotkeys;
using Centerly.Core.Settings;
using Serilog;

namespace Centerly.App.Services;

internal enum HotkeyAction
{
    CenterActiveWindow,
    MoveToNextMonitor,
    MoveToPreviousMonitor,
    Undo,
}

internal enum HotkeyChangeResult
{
    Applied,

    /// <summary>Another application or Windows owns the combination.</summary>
    InUseElsewhere,

    /// <summary>The combination is already assigned to another Centerly action.</summary>
    InUseByOtherAction,

    Failed,
}

/// <summary>Owns the global hotkeys and the automatic centering of new windows.</summary>
/// <remarks>All members must be called on the UI thread; centering itself runs on worker threads.</remarks>
internal sealed class CenteringService : IDisposable
{
    private static readonly ILogger Log = Serilog.Log.ForContext<CenteringService>();

    private readonly SettingsService _settings;
    private readonly HotkeyManager _hotkeys = new();
    private readonly WindowPositionHistory _history = new();
    private readonly WindowCenterer _centerer;
    private readonly AutoCenterEngine _autoCenter;

    private readonly Dictionary<HotkeyAction, int> _registeredIds = [];
    private readonly Dictionary<HotkeyAction, HotkeyRegistrationStatus> _statuses = [];
    private int _isBusy;
    private bool _isPaused;
    private bool _isSuspended;

    public CenteringService(SettingsService settings)
    {
        _settings = settings;
        _hotkeys.CallbackFailed += (_, ex) => Log.Error(ex, "Hotkey callback failed");

        // One history for both: undo also reverts automatic centering.
        _centerer = new WindowCenterer(_history);
        _autoCenter = new AutoCenterEngine(
            () => _settings.Current.Rules, () => _settings.Current.AutoCenterMode, () => _settings.Current.Oversize, _history);
        _autoCenter.WindowProcessed += OnAutoCentered;
        _autoCenter.Error += (_, ex) => Log.Error(ex, "Auto-centering failed");
        _settings.Changed += (_, _) => RefreshAutoCenter();
    }

    /// <summary>User-controlled pause (tray menu / settings toggle).</summary>
    public bool IsPaused
    {
        get => _isPaused;
        set
        {
            if (_isPaused == value) return;
            _isPaused = value;
            Log.Information("Centering {State}", value ? "paused" : "resumed");
            RefreshRegistrations();
            RefreshAutoCenter();
        }
    }

    public event EventHandler? StateChanged;

    /// <summary>Raised on a worker thread after each centering attempt, hotkey-triggered or automatic.</summary>
    public event EventHandler<CenterResult>? Centered;

    public void Start()
    {
        RefreshRegistrations();
        RefreshAutoCenter();
    }

    public Hotkey GetHotkey(HotkeyAction action) => GetHotkey(_settings.Current, action);

    /// <summary>Whether the hotkey for <paramref name="action"/> could be registered (true when it is not set or paused).</summary>
    public HotkeyRegistrationStatus GetStatus(HotkeyAction action) =>
        _statuses.GetValueOrDefault(action, HotkeyRegistrationStatus.Registered);

    /// <summary>
    /// Temporarily releases all hotkeys, e.g. while the user records a new combination:
    /// otherwise pressing a current one would trigger it instead of reaching the input box.
    /// </summary>
    public void SetSuspended(bool suspended)
    {
        if (_isSuspended == suspended) return;
        _isSuspended = suspended;
        RefreshRegistrations();
    }

    /// <summary>Validates and saves a new hotkey; the old one stays active if the new one cannot be registered.</summary>
    public HotkeyChangeResult ChangeHotkey(HotkeyAction action, Hotkey hotkey)
    {
        if (hotkey == GetHotkey(action))
            return HotkeyChangeResult.Applied;

        if (!hotkey.IsNone)
        {
            if (Enum.GetValues<HotkeyAction>().Any(other => other != action && GetHotkey(other) == hotkey))
                return HotkeyChangeResult.InUseByOtherAction;

            // Probe first (even while paused or suspended) so a conflicting combination never replaces a working one.
            var probe = _hotkeys.Register(hotkey, static () => { }, out var probeId);
            if (probe != HotkeyRegistrationStatus.Registered)
            {
                Log.Warning("Hotkey {Hotkey} for {Action} rejected: {Status}", hotkey, action, probe);
                return probe == HotkeyRegistrationStatus.AlreadyInUse ? HotkeyChangeResult.InUseElsewhere : HotkeyChangeResult.Failed;
            }

            _hotkeys.Unregister(probeId);
        }

        _settings.Update(s => action switch
        {
            HotkeyAction.CenterActiveWindow => s with { CenterHotkey = hotkey },
            HotkeyAction.MoveToNextMonitor => s with { NextMonitorHotkey = hotkey },
            HotkeyAction.MoveToPreviousMonitor => s with { PreviousMonitorHotkey = hotkey },
            HotkeyAction.Undo => s with { UndoHotkey = hotkey },
            _ => throw new InvalidEnumArgumentException(nameof(action), (int)action, typeof(HotkeyAction)),
        });
        Log.Information("{Action} hotkey changed to {Hotkey}", action, hotkey.IsNone ? "<none>" : hotkey);
        RefreshRegistrations();
        return HotkeyChangeResult.Applied;
    }

    /// <summary>Runs the action on the foreground window on a worker thread. Overlapping requests are dropped.</summary>
    public void Run(HotkeyAction action)
    {
        if (Interlocked.Exchange(ref _isBusy, 1) == 1)
            return;

        var oversize = _settings.Current.Oversize;
        _ = Task.Run(() =>
        {
            try
            {
                var result = action switch
                {
                    HotkeyAction.MoveToNextMonitor => _centerer.MoveForegroundToMonitor(MonitorDirection.Next, oversize),
                    HotkeyAction.MoveToPreviousMonitor => _centerer.MoveForegroundToMonitor(MonitorDirection.Previous, oversize),
                    HotkeyAction.Undo => _centerer.UndoForeground(),
                    _ => _centerer.CenterForeground(oversize),
                };
                if (result.IsSuccess)
                    Log.Debug("{Action}: {Result}", action, result);
                else
                    Log.Information("{Action}: {Result}", action, result);
                Centered?.Invoke(this, result);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Action} failed", action);
            }
            finally
            {
                Volatile.Write(ref _isBusy, 0);
            }
        });
    }

    public void Dispose()
    {
        _autoCenter.Dispose();
        _hotkeys.Dispose();
    }

    private static Hotkey GetHotkey(AppSettings settings, HotkeyAction action) => action switch
    {
        HotkeyAction.CenterActiveWindow => settings.CenterHotkey,
        HotkeyAction.MoveToNextMonitor => settings.NextMonitorHotkey,
        HotkeyAction.MoveToPreviousMonitor => settings.PreviousMonitorHotkey,
        HotkeyAction.Undo => settings.UndoHotkey,
        _ => Hotkey.None,
    };

    private void RefreshAutoCenter()
    {
        var shouldRun = !_isPaused && RuleMatcher.IsActive(_settings.Current.AutoCenterMode, _settings.Current.Rules);
        if (shouldRun == _autoCenter.IsRunning)
            return;

        if (shouldRun)
        {
            try
            {
                _autoCenter.Start();
                Log.Information("Auto-centering started");
            }
            catch (Win32Exception ex)
            {
                Log.Error(ex, "Cannot start auto-centering");
            }
        }
        else
        {
            _autoCenter.Stop();
            Log.Information("Auto-centering stopped");
        }
    }

    private void OnAutoCentered(object? sender, AutoCenterEvent e)
    {
        Log.Information("Auto-center ({Rule}{Correction}): {Result}",
            e.Rule.ProcessName, e.IsCorrection ? ", correction" : "", e.Result);

        // With all apps centered, elevated windows (Task Manager, installers) are routine: skip them quietly instead
        // of telling the user to restart as administrator. Explicitly listed apps still get the hint.
        if (e.IsListed || e.Result.Outcome != CenterOutcome.AccessDenied)
            Centered?.Invoke(this, e.Result);
    }

    private void RefreshRegistrations()
    {
        foreach (var id in _registeredIds.Values)
            _hotkeys.Unregister(id);
        _registeredIds.Clear();
        _statuses.Clear();

        if (!_isPaused && !_isSuspended)
        {
            foreach (var action in Enum.GetValues<HotkeyAction>())
            {
                var hotkey = GetHotkey(action);
                if (hotkey.IsNone)
                    continue;

                var status = _hotkeys.Register(hotkey, () => Run(action), out var id);
                _statuses[action] = status;
                if (status == HotkeyRegistrationStatus.Registered)
                {
                    _registeredIds[action] = id;
                    Log.Information("Registered {Action} hotkey {Hotkey}", action, hotkey);
                }
                else
                {
                    Log.Warning("Cannot register {Action} hotkey {Hotkey}: {Status}", action, hotkey, status);
                }
            }
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
