using System.Threading.Channels;
using Centerly.Core.Centering;
using Centerly.Core.Geometry;
using Centerly.Core.Interop;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace Centerly.Core.AutoCenter;

/// <param name="Rule">Rule that matched the window.</param>
/// <param name="Result">Centering outcome.</param>
/// <param name="IsCorrection">True when the window was re-centered because the app moved it after it appeared.</param>
public sealed record AutoCenterEvent(AppRule Rule, CenterResult Result, bool IsCorrection)
{
    /// <summary>False when the app has no rule and was centered only because all apps are.</summary>
    public bool IsListed => !ReferenceEquals(Rule, RuleMatcher.DefaultRule);
}

/// <summary>
/// Centers newly shown windows of the applications selected by the rules and the mode.
/// </summary>
/// <remarks>
/// WinEvent hooks run on the UI thread and only enqueue events; matching and centering happen on a single background
/// worker, so the UI never waits for a foreign window and the worker state needs no locks.
/// <para>
/// Many apps restore their saved position or resize themselves right after showing the window, undoing the centering.
/// Each centered window is therefore re-checked a few times during the first second and re-centered if the app moved
/// it, with a cap on corrections so we never fight an app that insists on its own position. A user drag stops the
/// re-checks immediately.
/// </para>
/// <para>
/// Only windows created after <see cref="Start"/> count as new. Windows that already exist (hidden in the tray, on
/// another virtual desktop, opened while paused) fire SHOW/UNCLOAKED when they reappear and must not jump around.
/// </para>
/// </remarks>
public sealed class AutoCenterEngine : IDisposable
{
    private static readonly TimeSpan[] RecheckDelays =
        [TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(300), TimeSpan.FromMilliseconds(1000)];

    // Waits for a UWP frame's content to attach: ~3 s in total.
    private static readonly TimeSpan[] ResolveRetryDelays =
    [
        TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(200),
        TimeSpan.FromMilliseconds(400), TimeSpan.FromMilliseconds(800), TimeSpan.FromMilliseconds(1600),
    ];

    private const string ApplicationFrameClass = "ApplicationFrameWindow";

    private const int MaxCorrections = 3;

    // Smaller windows are helper/placeholder windows or not laid out yet.
    private const int MinWidth = 120;
    private const int MinHeight = 80;

    private const int MinPruneThreshold = 512;

    private readonly Func<IReadOnlyList<AppRule>> _rules;
    private readonly Func<AutoCenterMode> _mode;
    private readonly Func<OversizeBehavior> _oversize;
    private readonly WindowCenterer _centerer;
    private readonly Channel<WindowEvent> _events =
        Channel.CreateUnbounded<WindowEvent>(new UnboundedChannelOptions { SingleReader = true });
    private readonly Task _worker;

    // Hooks: UI thread only.
    private readonly List<WinEventHook> _hooks = [];

    // Worker state: touched only from the worker loop.
    private readonly Dictionary<nint, TrackedWindow> _tracked = [];
    private readonly HashSet<nint> _handledWindows = [];
    private readonly HashSet<(uint ProcessId, long StartTime)> _handledLaunches = [];
    private int _pruneThreshold = MinPruneThreshold;

    private volatile bool _isRunning;

    /// <param name="rules">Current rules; read on the worker thread for every new window.</param>
    /// <param name="mode">Current mode; read on the worker thread for every new window.</param>
    /// <param name="oversize">Current oversize behavior; read on the worker thread.</param>
    /// <param name="history">Records the moves so the user can undo them.</param>
    public AutoCenterEngine(
        Func<IReadOnlyList<AppRule>> rules,
        Func<AutoCenterMode> mode,
        Func<OversizeBehavior> oversize,
        WindowPositionHistory? history = null)
    {
        _rules = rules;
        _mode = mode;
        _oversize = oversize;
        _centerer = new WindowCenterer(history);
        _worker = Task.Run(RunWorkerAsync);
    }

    public bool IsRunning => _isRunning;

    /// <summary>Raised on the worker thread after each centering attempt of a matched window.</summary>
    public event EventHandler<AutoCenterEvent>? WindowProcessed;

    /// <summary>Raised on the UI or worker thread when processing an event throws.</summary>
    public event EventHandler<Exception>? Error;

    /// <summary>Installs the window event hooks. Call on the UI thread.</summary>
    public void Start()
    {
        if (_isRunning)
            return;

        try
        {
            _hooks.Add(new WinEventHook(PInvoke.EVENT_OBJECT_SHOW, PInvoke.EVENT_OBJECT_SHOW, OnShown, ReportError));
            // UWP frames are shown while still cloaked and become visible with UNCLOAKED.
            _hooks.Add(new WinEventHook(PInvoke.EVENT_OBJECT_UNCLOAKED, PInvoke.EVENT_OBJECT_UNCLOAKED, OnShown, ReportError));
            _hooks.Add(new WinEventHook(PInvoke.EVENT_SYSTEM_MOVESIZESTART, PInvoke.EVENT_SYSTEM_MOVESIZESTART, OnMoveSizeStart, ReportError));
        }
        catch
        {
            StopHooks();
            throw;
        }

        _isRunning = true;

        // Queued after the flag is set (the worker drops events while stopped) and handled in order.
        PInvoke.EnumWindows((hwnd, _) =>
        {
            _events.Writer.TryWrite(new WindowEvent(WindowEventKind.AlreadyOpen, hwnd));
            return true;
        }, 0);
    }

    /// <summary>Removes the hooks. Pending re-checks are dropped. Call on the UI thread.</summary>
    public void Stop()
    {
        _isRunning = false;
        StopHooks();
    }

    public void Dispose()
    {
        Stop();
        _events.Writer.TryComplete();
    }

    private void OnShown(uint eventType, HWND hwnd)
    {
        // Cheap pre-filter on the UI thread: SHOW fires for every child control too.
        if (PInvoke.GetAncestor(hwnd, Windows.Win32.UI.WindowsAndMessaging.GET_ANCESTOR_FLAGS.GA_ROOT) == hwnd)
            _events.Writer.TryWrite(new WindowEvent(WindowEventKind.Shown, hwnd));
    }

    private void OnMoveSizeStart(uint eventType, HWND hwnd) =>
        _events.Writer.TryWrite(new WindowEvent(WindowEventKind.UserMoveStarted, hwnd));

    private async Task RunWorkerAsync()
    {
        await foreach (var e in _events.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            if (!_isRunning)
            {
                _tracked.Clear();
                continue;
            }

            try
            {
                switch (e.Kind)
                {
                    case WindowEventKind.AlreadyOpen:
                        _handledWindows.Add(Win32Window.ToKey(e.Window));
                        break;
                    case WindowEventKind.Shown:
                        HandleShown(e.Window, e.Step);
                        break;
                    case WindowEventKind.UserMoveStarted:
                        _tracked.Remove(e.Window);
                        break;
                    case WindowEventKind.Recheck:
                        HandleRecheck(e.Window, e.Step);
                        break;
                }
            }
            catch (Exception ex)
            {
                ReportError(ex);
            }
        }
    }

    private void HandleShown(HWND hwnd, int attempt)
    {
        var key = Win32Window.ToKey(hwnd);
        if (_handledWindows.Contains(key) || !Win32Window.IsAppWindow(hwnd, out var isOwned))
            return;

        if (ProcessIdentity.FromWindow(hwnd) is not { IsValid: true } process)
        {
            // A UWP frame (e.g. Settings) is shown and uncloaked before the app's CoreWindow is attached, and no
            // further event follows; poll until the hosted app can be identified.
            if (attempt < ResolveRetryDelays.Length && Win32Window.GetClassName(hwnd) == ApplicationFrameClass)
                ScheduleEvent(new WindowEvent(WindowEventKind.Shown, hwnd, attempt + 1), ResolveRetryDelays[attempt]);
            return;
        }

        var rule = RuleMatcher.Find(_rules(), _mode(), process.ProcessName, isOwned);
        if (rule is null)
            return;

        PruneIfNeeded();
        _handledWindows.Add(key);

        var launch = (process.ProcessId, process.StartTime);
        if (rule.OnlyFirstWindow && !_handledLaunches.Add(launch))
            return;

        var tracked = new TrackedWindow(rule);
        _tracked[key] = tracked;
        TryCenter(hwnd, tracked, isCorrection: false);
        ScheduleRecheck(hwnd, 0);
    }

    private void HandleRecheck(HWND hwnd, int step)
    {
        var key = Win32Window.ToKey(hwnd);
        if (!_tracked.TryGetValue(key, out var tracked))
            return;

        if (!PInvoke.IsWindow(hwnd))
        {
            _tracked.Remove(key);
            return;
        }

        if (Win32Window.TryGetWindowRect(hwnd, out var rect) && rect != tracked.ExpectedRect && tracked.Corrections < MaxCorrections)
        {
            tracked.Corrections++;
            TryCenter(hwnd, tracked, isCorrection: tracked.ExpectedRect is not null);
        }

        if (step + 1 < RecheckDelays.Length)
            ScheduleRecheck(hwnd, step + 1);
        else
            _tracked.Remove(key);
    }

    private void TryCenter(HWND hwnd, TrackedWindow tracked, bool isCorrection)
    {
        // Not laid out yet: leave ExpectedRect unset so the next re-check tries again.
        if (!Win32Window.TryGetGeometry(hwnd, out var geometry)
            || geometry.VisibleRect.Width < MinWidth || geometry.VisibleRect.Height < MinHeight)
        {
            return;
        }

        var result = _centerer.Center(Win32Window.ToKey(hwnd), _oversize());
        if (Win32Window.TryGetWindowRect(hwnd, out var rect))
            tracked.ExpectedRect = rect;

        // Maximized/fullscreen or an elevated window: nothing to correct later.
        if (result.Outcome is CenterOutcome.NotEligible or CenterOutcome.AccessDenied or CenterOutcome.NoWindow)
            _tracked.Remove(Win32Window.ToKey(hwnd));

        if (result.Outcome != CenterOutcome.AlreadyCentered || !isCorrection)
            WindowProcessed?.Invoke(this, new AutoCenterEvent(tracked.Rule, result, isCorrection));
    }

    private void ScheduleRecheck(HWND hwnd, int step)
    {
        if (_tracked.ContainsKey(Win32Window.ToKey(hwnd)))
            ScheduleEvent(new WindowEvent(WindowEventKind.Recheck, hwnd, step), RecheckDelays[step]);
    }

    private void ScheduleEvent(WindowEvent e, TimeSpan delay) =>
        _ = Task.Delay(delay).ContinueWith(_ => _events.Writer.TryWrite(e), TaskScheduler.Default);

    private void PruneIfNeeded()
    {
        // Already open windows alone can exceed the minimum; grow the threshold so pruning stays occasional.
        if (_handledWindows.Count > _pruneThreshold)
        {
            _handledWindows.RemoveWhere(h => !PInvoke.IsWindow((HWND)h));
            _pruneThreshold = Math.Max(MinPruneThreshold, _handledWindows.Count * 2);
        }

        if (_handledLaunches.Count > MinPruneThreshold / 4)
            _handledLaunches.RemoveWhere(l => ProcessIdentity.FromProcessId(l.ProcessId)?.StartTime != l.StartTime);
    }

    private void StopHooks()
    {
        foreach (var hook in _hooks)
            hook.Dispose();
        _hooks.Clear();
    }

    private void ReportError(Exception ex) => Error?.Invoke(this, ex);

    private enum WindowEventKind
    {
        /// <summary>Existed when the engine started, never treat it as new.</summary>
        AlreadyOpen,
        Shown,
        UserMoveStarted,
        Recheck,
    }

    private readonly record struct WindowEvent(WindowEventKind Kind, HWND Window, int Step = 0);

    private sealed class TrackedWindow(AppRule rule)
    {
        public AppRule Rule { get; } = rule;

        /// <summary>Window rect right after our last move; null while we have not moved it yet.</summary>
        public RectI? ExpectedRect { get; set; }

        public int Corrections { get; set; }
    }
}
