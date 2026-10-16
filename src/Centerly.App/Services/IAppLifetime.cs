namespace Centerly.App.Services;

/// <summary>Application-level actions the UI can request; implemented by <see cref="App"/>.</summary>
internal interface IAppLifetime
{
    /// <summary>Starts a new instance (with the same rights) and exits this one.</summary>
    void Restart();

    /// <summary>Starts an elevated instance (UAC prompt) and exits this one; does nothing if the prompt is declined.</summary>
    void RestartElevated();
}
