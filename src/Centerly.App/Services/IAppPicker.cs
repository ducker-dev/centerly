using Centerly.Core.AutoCenter;

namespace Centerly.App.Services;

/// <summary>UI for choosing an application to add a rule for; implemented by the main window.</summary>
internal interface IAppPicker
{
    /// <summary>Shows running applications; null if the user cancelled.</summary>
    Task<RunningApp?> PickRunningAppAsync(IEnumerable<string> excludedProcessNames);

    /// <summary>Shows a file dialog for an .exe; null if the user cancelled.</summary>
    string? PickExecutable();
}
