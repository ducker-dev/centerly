using System.ComponentModel;
using System.Diagnostics;
using System.Security;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32;
using Serilog;

namespace Centerly.App.Services;

internal enum AutostartMode
{
    Off,

    /// <summary>HKCU Run key: starts with the user's normal rights.</summary>
    Normal,

    /// <summary>Task Scheduler task with highest privileges: can move windows of elevated apps, no UAC prompt at logon.</summary>
    Elevated,
}

/// <summary>
/// Start with Windows. The actual system state (registry value / scheduled task) is the source of truth,
/// so the UI always shows what will really happen at the next logon.
/// </summary>
internal static class Autostart
{
    public const string BackgroundArgument = "--background";

    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Centerly";
    private const string TaskName = "Centerly";

    private static readonly ILogger Log = Serilog.Log.ForContext(typeof(Autostart));

    public static string ExecutablePath => Environment.ProcessPath!;

    public static bool IsProcessElevated
    {
        get
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
    }

    public static AutostartMode GetMode()
    {
        if (TaskExists())
            return AutostartMode.Elevated;

        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        return key?.GetValue(ValueName) is string ? AutostartMode.Normal : AutostartMode.Off;
    }

    /// <summary>Switches the mode. Creating or deleting the elevated task shows a UAC prompt.</summary>
    /// <returns>False if the user declined the UAC prompt or the change failed (details are logged).</returns>
    public static bool SetMode(AutostartMode mode)
    {
        var current = GetMode();
        if (current == mode)
            return true;

        // Task first: it needs elevation and may be cancelled, then nothing else should change.
        if (current == AutostartMode.Elevated && !RunElevated(ElevatedCommands.DeleteTask))
            return false;
        if (mode == AutostartMode.Elevated && !RunElevated(ElevatedCommands.CreateTask))
            return false;

        SetRunValue(mode == AutostartMode.Normal);
        Log.Information("Autostart changed from {From} to {To}", current, mode);
        return true;
    }

    /// <summary>Velopack uninstall hook: best effort, must be quick and never throw.</summary>
    public static void RemoveForUninstall()
    {
        try
        {
            SetRunValue(false);
            if (TaskExists())
                RunElevated(ElevatedCommands.DeleteTask);
        }
        catch (Exception)
        {
            // Uninstall must proceed regardless.
        }
    }

    /// <summary>
    /// Starts a replacement instance that waits for this one to exit. Elevated shows a UAC prompt.
    /// The caller must shut down after a true result.
    /// </summary>
    public static bool TryStartNewInstance(bool elevated)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo(ExecutablePath)
            {
                UseShellExecute = true,
                Verb = elevated ? "runas" : string.Empty,
                ArgumentList = { ElevatedCommands.WaitForPreviousInstance },
            });
            return process is not null;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorCancelled)
        {
            return false;
        }
    }

    // Called from the elevated helper process.
    internal static void CreateTask()
    {
        var xmlPath = Path.Combine(Path.GetTempPath(), $"Centerly-task-{Guid.NewGuid():N}.xml");
        try
        {
            // schtasks requires UTF-16 for task XML.
            File.WriteAllText(xmlPath, BuildTaskXml(), Encoding.Unicode);
            EnsureSuccess(RunSchtasks("/Create", "/TN", TaskName, "/XML", xmlPath, "/F"));
        }
        finally
        {
            File.Delete(xmlPath);
        }
    }

    // Called from the elevated helper process.
    internal static void DeleteTask() => EnsureSuccess(RunSchtasks("/Delete", "/TN", TaskName, "/F"));

    private static void EnsureSuccess(int exitCode)
    {
        if (exitCode != 0)
            throw new InvalidOperationException($"schtasks.exe exited with code {exitCode}");
    }

    private const int ErrorCancelled = 1223;

    private static void SetRunValue(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
        if (enabled)
            key.SetValue(ValueName, $"\"{ExecutablePath}\" {BackgroundArgument}");
        else
            key.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    private static bool TaskExists()
    {
        try
        {
            return RunSchtasks("/Query", "/TN", TaskName) == 0;
        }
        catch (Win32Exception)
        {
            return false;
        }
    }

    private static bool RunElevated(string command)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo(ExecutablePath)
            {
                UseShellExecute = true,
                Verb = "runas",
                ArgumentList = { command },
                WindowStyle = ProcessWindowStyle.Hidden,
            });
            if (process is null)
                return false;

            process.WaitForExit();
            if (process.ExitCode != 0)
                Log.Warning("Elevated command {Command} failed with exit code {Code}", command, process.ExitCode);
            return process.ExitCode == 0;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorCancelled)
        {
            Log.Information("UAC prompt for {Command} was cancelled", command);
            return false;
        }
    }

    private static int RunSchtasks(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("schtasks.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo)!;
        process.StandardOutput.ReadToEnd();
        process.StandardError.ReadToEnd();
        process.WaitForExit();
        return process.ExitCode;
    }

    private static string BuildTaskXml()
    {
        var user = SecurityElement.Escape(WindowsIdentity.GetCurrent().Name);
        var command = SecurityElement.Escape(ExecutablePath);
        // Defaults of Task Scheduler that would break a tray app are overridden explicitly:
        // 72h execution limit, no start on battery, below-normal priority (7).
        return $"""
            <?xml version="1.0" encoding="UTF-16"?>
            <Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
              <RegistrationInfo>
                <Description>Centerly — start at logon with administrator rights</Description>
              </RegistrationInfo>
              <Triggers>
                <LogonTrigger>
                  <Enabled>true</Enabled>
                  <UserId>{user}</UserId>
                </LogonTrigger>
              </Triggers>
              <Principals>
                <Principal id="Author">
                  <UserId>{user}</UserId>
                  <LogonType>InteractiveToken</LogonType>
                  <RunLevel>HighestAvailable</RunLevel>
                </Principal>
              </Principals>
              <Settings>
                <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
                <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
                <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
                <AllowHardTerminate>true</AllowHardTerminate>
                <StartWhenAvailable>false</StartWhenAvailable>
                <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
                <IdleSettings>
                  <StopOnIdleEnd>false</StopOnIdleEnd>
                  <RestartOnIdle>false</RestartOnIdle>
                </IdleSettings>
                <AllowStartOnDemand>true</AllowStartOnDemand>
                <Enabled>true</Enabled>
                <Hidden>false</Hidden>
                <RunOnlyIfIdle>false</RunOnlyIfIdle>
                <WakeToRun>false</WakeToRun>
                <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
                <Priority>4</Priority>
              </Settings>
              <Actions Context="Author">
                <Exec>
                  <Command>{command}</Command>
                  <Arguments>{BackgroundArgument}</Arguments>
                </Exec>
              </Actions>
            </Task>
            """;
    }
}
