using System.Reflection;
using Centerly.Core.Settings;

namespace Centerly.App.Services;

internal static class AppPaths
{
    public static string SettingsFile => SettingsStore.DefaultFilePath;

    public static string LogsDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Centerly", "logs");

    /// <summary>"0.1.0" without the "+commit" build metadata.</summary>
    public static string Version { get; } =
        (Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0")
        .Split('+')[0];
}
