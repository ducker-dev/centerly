using System.Collections.Concurrent;
using Centerly.Core.Geometry;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace Centerly.Core.Centering;

/// <param name="Rect">GetWindowRect of the window before the move.</param>
/// <param name="IsMaximized">The window was maximized; <paramref name="Rect"/> then only identifies its monitor.</param>
public readonly record struct SavedPosition(RectI Rect, bool IsMaximized);

/// <summary>Where each window was before Centerly last moved it, so the move can be undone.</summary>
/// <remarks>Thread-safe: the hotkey worker and the auto-centering worker both record moves.</remarks>
public sealed class WindowPositionHistory(Func<nint, bool> isAlive)
{
    private const int MinPruneThreshold = 256;

    private readonly ConcurrentDictionary<nint, SavedPosition> _positions = new();
    private int _pruneThreshold = MinPruneThreshold;

    public WindowPositionHistory()
        : this(static handle => PInvoke.IsWindow((HWND)handle))
    {
    }

    public int Count => _positions.Count;

    public void Remember(nint window, SavedPosition position)
    {
        // Windows we moved once and that were closed since; there is no event for that worth hooking.
        if (_positions.Count >= Volatile.Read(ref _pruneThreshold))
        {
            foreach (var handle in _positions.Keys)
            {
                if (!isAlive(handle))
                    _positions.TryRemove(handle, out _);
            }

            // Many live windows: grow the threshold so pruning stays occasional. A race here only prunes twice.
            Volatile.Write(ref _pruneThreshold, Math.Max(MinPruneThreshold, _positions.Count * 2));
        }

        _positions[window] = position;
    }

    public bool TryGet(nint window, out SavedPosition position) => _positions.TryGetValue(window, out position);
}
