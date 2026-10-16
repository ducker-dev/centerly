namespace Centerly.Core.Geometry;

/// <param name="Bounds">Whole monitor in physical pixels.</param>
/// <param name="WorkArea">Monitor without the taskbar and docked toolbars.</param>
public readonly record struct MonitorArea(RectI Bounds, RectI WorkArea);

public static class MonitorMath
{
    /// <summary>
    /// The monitor after <paramref name="current"/> in reading order (left to right, then top to bottom), wrapping around.
    /// Returns <paramref name="current"/> itself when it is the only one or is not in the list.
    /// </summary>
    public static MonitorArea Next(IReadOnlyList<MonitorArea> monitors, MonitorArea current) => Step(monitors, current, 1);

    /// <summary>The monitor before <paramref name="current"/>, the reverse of <see cref="Next"/>.</summary>
    public static MonitorArea Previous(IReadOnlyList<MonitorArea> monitors, MonitorArea current) => Step(monitors, current, -1);

    /// <summary>The monitor containing the center of <paramref name="rect"/>, or the one nearest to it.</summary>
    public static MonitorArea? Containing(IReadOnlyList<MonitorArea> monitors, RectI rect)
    {
        var x = rect.Left + rect.Width / 2;
        var y = rect.Top + rect.Height / 2;
        return monitors.Count == 0
            ? null
            : monitors.MinBy(m => DistanceSquared(m.Bounds, x, y));
    }

    private static MonitorArea Step(IReadOnlyList<MonitorArea> monitors, MonitorArea current, int step)
    {
        var ordered = monitors
            .OrderBy(m => m.Bounds.Left)
            .ThenBy(m => m.Bounds.Top)
            .ToList();

        var index = ordered.FindIndex(m => m.Bounds == current.Bounds);
        return index < 0 || ordered.Count < 2 ? current : ordered[(index + step + ordered.Count) % ordered.Count];
    }

    // Zero for points inside the rectangle.
    private static long DistanceSquared(RectI rect, int x, int y)
    {
        long dx = Math.Max(0, Math.Max(rect.Left - x, x - (rect.Right - 1)));
        long dy = Math.Max(0, Math.Max(rect.Top - y, y - (rect.Bottom - 1)));
        return dx * dx + dy * dy;
    }
}
