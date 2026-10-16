using Centerly.Core.Settings;
using Serilog;

namespace Centerly.App.Services;

/// <summary>Holds the current settings in memory and persists every change.</summary>
internal sealed class SettingsService
{
    private static readonly ILogger Log = Serilog.Log.ForContext<SettingsService>();

    private readonly SettingsStore _store;

    public SettingsService(SettingsStore store)
    {
        _store = store;
        LoadResult = store.Load();
        Current = LoadResult.Settings;

        switch (LoadResult.Status)
        {
            case SettingsLoadStatus.Loaded:
                Log.Information("Settings loaded from {Path}", store.FilePath);
                break;
            case SettingsLoadStatus.Missing:
                Log.Information("No settings file at {Path}, creating with defaults", store.FilePath);
                TrySave(Current);
                break;
            case SettingsLoadStatus.Corrupted:
                Log.Warning(LoadResult.Error, "Settings file is corrupted, moved to {Backup}, using defaults", LoadResult.BackupPath);
                break;
        }
    }

    public SettingsLoadResult LoadResult { get; }

    public AppSettings Current { get; private set; }

    public bool IsFirstRun => LoadResult.Status == SettingsLoadStatus.Missing;

    public event EventHandler<AppSettings>? Changed;

    public void Update(Func<AppSettings, AppSettings> change)
    {
        var updated = change(Current);
        if (updated == Current)
            return;

        Current = updated;
        TrySave(updated);
        Changed?.Invoke(this, updated);
    }

    private void TrySave(AppSettings settings)
    {
        try
        {
            _store.Save(settings);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Keep working with the in-memory value; the next change will try to save again.
            Log.Error(ex, "Failed to save settings to {Path}", _store.FilePath);
        }
    }
}
