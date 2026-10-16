using System.Reflection;
using Serilog;
using Velopack;
using Velopack.Sources;

namespace Centerly.App.Services;

/// <summary>Checks GitHub releases for a new version and installs it (Velopack).</summary>
/// <remarks>
/// Disabled when the build has no repository URL or the app does not run from a Velopack installation
/// (e.g. from bin/ during development).
/// </remarks>
internal sealed class UpdateService : IDisposable
{
    private static readonly ILogger Log = Serilog.Log.ForContext<UpdateService>();
    private static readonly TimeSpan FirstCheckDelay = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(12);

    private readonly UpdateManager? _manager;
    private readonly CancellationTokenSource _cts = new();

    public UpdateService()
    {
        var repositoryUrl = Assembly.GetEntryAssembly()?
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "UpdateRepositoryUrl")?.Value;
        if (string.IsNullOrWhiteSpace(repositoryUrl))
            return;

        try
        {
            var manager = new UpdateManager(new GithubSource(repositoryUrl, null, false, null), null, null);
            if (manager.IsInstalled)
                _manager = manager;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Update manager is unavailable");
        }
    }

    public bool IsSupported => _manager is not null;

    /// <summary>Version found by the last successful check, if newer than the running one.</summary>
    public UpdateInfo? Available { get; private set; }

    /// <summary>Raised on the UI thread's synchronization context when <see cref="Available"/> changes.</summary>
    public event EventHandler? AvailableChanged;

    /// <summary>Checks periodically while <paramref name="isEnabled"/> returns true. Call once from the UI thread.</summary>
    public void StartBackgroundChecks(Func<bool> isEnabled)
    {
        if (_manager is null)
            return;

        var token = _cts.Token;
        _ = RunAsync();

        async Task RunAsync()
        {
            try
            {
                await Task.Delay(FirstCheckDelay, token);
                using var timer = new PeriodicTimer(CheckInterval);
                do
                {
                    if (isEnabled())
                        await CheckAsync();
                }
                while (await timer.WaitForNextTickAsync(token));
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    /// <summary>Returns the available update or null; failures (offline, rate limits) are logged, not thrown.</summary>
    public async Task<UpdateInfo?> CheckAsync()
    {
        if (_manager is null)
            return null;

        try
        {
            var update = await _manager.CheckForUpdatesAsync();
            Log.Information("Update check: {Result}", update?.TargetFullRelease.Version.ToString() ?? "up to date");
            if (update?.TargetFullRelease.Version != Available?.TargetFullRelease.Version)
            {
                Available = update;
                AvailableChanged?.Invoke(this, EventArgs.Empty);
            }

            return update;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Update check failed");
            return null;
        }
    }

    /// <summary>Downloads the available update and restarts into it. Returns false if it failed.</summary>
    public async Task<bool> InstallAndRestartAsync(Action<int>? progress = null)
    {
        if (_manager is null || Available is null)
            return false;

        try
        {
            await _manager.DownloadUpdatesAsync(Available, progress, _cts.Token);
            _manager.ApplyUpdatesAndRestart(Available.TargetFullRelease, []);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Update installation failed");
            return false;
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }
}
