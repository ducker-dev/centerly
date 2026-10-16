using System.Security.AccessControl;
using System.Security.Principal;

namespace Centerly.App.Services;

/// <summary>
/// Ensures one running instance per user session. A second launch signals the first one to show its window and exits.
/// </summary>
/// <remarks>
/// The kernel objects get an explicit DACL for the current user: with the default one, objects created by an elevated
/// instance cannot be opened by a non-elevated launch (and vice versa would work, which makes it confusing).
/// </remarks>
internal sealed class SingleInstance : IDisposable
{
    private const string Id = "Centerly-7d3f2a9c-41b6-4e0b-9a51-2c8e6f0d4b17";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activateEvent;
    private RegisteredWaitHandle? _waitRegistration;

    private SingleInstance(Mutex mutex, EventWaitHandle activateEvent)
    {
        _mutex = mutex;
        _activateEvent = activateEvent;
    }

    /// <summary>
    /// Returns the guard for the first instance, or null if another instance keeps running for longer than
    /// <paramref name="wait"/> (in which case it has been asked to activate).
    /// </summary>
    public static SingleInstance? TryAcquire(TimeSpan wait)
    {
        var user = WindowsIdentity.GetCurrent().User!;

        var mutexSecurity = new MutexSecurity();
        mutexSecurity.AddAccessRule(new MutexAccessRule(user, MutexRights.FullControl, AccessControlType.Allow));
        var mutex = MutexAcl.Create(false, $@"Local\{Id}", out _, mutexSecurity);

        var eventSecurity = new EventWaitHandleSecurity();
        eventSecurity.AddAccessRule(new EventWaitHandleAccessRule(user, EventWaitHandleRights.FullControl, AccessControlType.Allow));
        var activateEvent = EventWaitHandleAcl.Create(false, EventResetMode.AutoReset, $@"Local\{Id}-activate", out _, eventSecurity);

        bool acquired;
        try
        {
            acquired = mutex.WaitOne(wait);
        }
        catch (AbandonedMutexException)
        {
            // The previous instance crashed; the mutex is ours now.
            acquired = true;
        }

        if (acquired)
            return new SingleInstance(mutex, activateEvent);

        activateEvent.Set();
        activateEvent.Dispose();
        mutex.Dispose();
        return null;
    }

    /// <summary>Invokes <paramref name="onActivate"/> (on a thread-pool thread) whenever another launch is attempted.</summary>
    public void ListenForActivation(Action onActivate)
    {
        _waitRegistration = ThreadPool.RegisterWaitForSingleObject(
            _activateEvent, (_, _) => onActivate(), null, Timeout.Infinite, executeOnlyOnce: false);
    }

    public void Dispose()
    {
        _waitRegistration?.Unregister(null);
        _activateEvent.Dispose();
        _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}
