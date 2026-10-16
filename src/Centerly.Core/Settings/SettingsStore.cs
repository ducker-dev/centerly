using System.Text.Json;

namespace Centerly.Core.Settings;

public enum SettingsLoadStatus
{
    Loaded,

    /// <summary>No settings file yet (first run), defaults are used.</summary>
    Missing,

    /// <summary>The file was unreadable; it was moved aside to <see cref="SettingsLoadResult.BackupPath"/> and defaults are used.</summary>
    Corrupted,
}

public sealed record SettingsLoadResult(AppSettings Settings, SettingsLoadStatus Status, string? BackupPath = null, Exception? Error = null);

/// <summary>Reads and writes <see cref="AppSettings"/> as JSON. Writes are atomic: a crash mid-save never leaves a broken file.</summary>
public sealed class SettingsStore(string filePath)
{
    public string FilePath { get; } = filePath;

    public static string DefaultFilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Centerly", "settings.json");

    public SettingsLoadResult Load()
    {
        if (!File.Exists(FilePath))
            return new(new AppSettings(), SettingsLoadStatus.Missing);

        try
        {
            using var stream = File.OpenRead(FilePath);
            var settings = JsonSerializer.Deserialize(stream, SettingsJsonContext.Readable.AppSettings)
                           ?? throw new JsonException("Settings file contains null.");
            return new(settings, SettingsLoadStatus.Loaded);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // Keep the broken file for the user/bug report instead of silently overwriting it on the next save.
            var backupPath = $"{FilePath}.{DateTime.Now:yyyyMMdd-HHmmss}.bad";
            try
            {
                File.Move(FilePath, backupPath, overwrite: true);
            }
            catch (Exception moveEx) when (moveEx is IOException or UnauthorizedAccessException)
            {
                backupPath = null;
            }

            return new(new AppSettings(), SettingsLoadStatus.Corrupted, backupPath, ex);
        }
    }

    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);

        var tempPath = FilePath + ".tmp";
        using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            JsonSerializer.Serialize(stream, settings, SettingsJsonContext.Readable.AppSettings);
            stream.Flush(flushToDisk: true);
        }

        // MoveFileEx(REPLACE_EXISTING) on the same volume is atomic on NTFS.
        File.Move(tempPath, FilePath, overwrite: true);
    }
}
