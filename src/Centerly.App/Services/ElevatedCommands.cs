namespace Centerly.App.Services;

/// <summary>
/// Command-line modes of Centerly.exe that do one job and exit. The app relaunches itself with them via
/// the "runas" verb when an operation needs administrator rights.
/// </summary>
internal static class ElevatedCommands
{
    public const string CreateTask = "--autostart-task-create";
    public const string DeleteTask = "--autostart-task-delete";

    /// <summary>Started elevated as a replacement of a running instance; waits until it exits.</summary>
    public const string WaitForPreviousInstance = "--wait-for-previous-instance";

    /// <returns>True if <paramref name="args"/> was a helper command (already executed), false to start the app.</returns>
    public static bool TryRun(string[] args, out int exitCode)
    {
        exitCode = 0;
        if (args.Length == 0)
            return false;

        try
        {
            switch (args[0])
            {
                case CreateTask:
                    Autostart.CreateTask();
                    return true;
                case DeleteTask:
                    Autostart.DeleteTask();
                    return true;
                default:
                    return false;
            }
        }
        catch (Exception)
        {
            exitCode = 1;
            return true;
        }
    }
}
