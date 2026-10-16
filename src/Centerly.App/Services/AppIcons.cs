using System.Collections.Concurrent;
using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Centerly.App.Services;

/// <summary>Icons and friendly names of executables, cached per path.</summary>
internal static class AppIcons
{
    private static readonly ConcurrentDictionary<string, ImageSource?> Cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Executable icon, or null when the file is missing or inaccessible (e.g. inside WindowsApps).</summary>
    public static ImageSource? Get(string? executablePath)
    {
        if (string.IsNullOrEmpty(executablePath))
            return null;

        return Cache.GetOrAdd(executablePath, static path =>
        {
            try
            {
                if (!File.Exists(path))
                    return null;

                using var icon = System.Drawing.Icon.ExtractAssociatedIcon(path);
                if (icon is null)
                    return null;

                var source = Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                source.Freeze();
                return source;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or System.ComponentModel.Win32Exception)
            {
                return null;
            }
        });
    }

    /// <summary>"Блокнот" from the file description, falling back to the file name without extension.</summary>
    public static string GetDisplayName(string? executablePath, string processName)
    {
        var fallback = Path.GetFileNameWithoutExtension(processName);
        if (string.IsNullOrEmpty(executablePath))
            return fallback;

        try
        {
            // Some apps (e.g. Store Notepad) put just "Notepad.exe" into the description; that is not a name.
            var info = FileVersionInfo.GetVersionInfo(executablePath);
            return new[] { info.FileDescription, info.ProductName }
                .Select(n => n?.Trim())
                .FirstOrDefault(n => !string.IsNullOrEmpty(n) && !n.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                ?? fallback;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return fallback;
        }
    }
}
