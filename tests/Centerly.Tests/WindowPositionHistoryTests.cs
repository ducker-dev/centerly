using Centerly.Core.Centering;
using Centerly.Core.Geometry;

namespace Centerly.Tests;

public class WindowPositionHistoryTests
{
    private static readonly SavedPosition Somewhere = new(RectI.FromSize(10, 20, 800, 600), IsMaximized: false);

    [Fact]
    public void Remembers_last_position_per_window()
    {
        var history = new WindowPositionHistory(static _ => true);
        var later = Somewhere with { Rect = RectI.FromSize(500, 400, 800, 600) };

        history.Remember(1, Somewhere);
        history.Remember(1, later);
        history.Remember(2, Somewhere);

        Assert.True(history.TryGet(1, out var first));
        Assert.Equal(later, first);
        Assert.True(history.TryGet(2, out var second));
        Assert.Equal(Somewhere, second);
        Assert.False(history.TryGet(3, out _));
    }

    [Fact]
    public void Drops_closed_windows_when_it_grows()
    {
        var alive = new HashSet<nint>();
        var history = new WindowPositionHistory(alive.Contains);
        for (nint handle = 1; handle <= 256; handle++)
            history.Remember(handle, Somewhere);

        alive.Add(256);
        history.Remember(257, Somewhere);

        Assert.True(history.TryGet(256, out _));
        Assert.True(history.TryGet(257, out _));
        Assert.False(history.TryGet(1, out _));
        Assert.Equal(2, history.Count);
    }

    [Fact]
    public void Does_not_rescan_on_every_move_when_many_windows_are_alive()
    {
        var checks = 0;
        var history = new WindowPositionHistory(_ => { checks++; return true; });
        for (nint handle = 1; handle <= 400; handle++)
            history.Remember(handle, Somewhere);

        // One scan at 256 entries, then the threshold doubles past 400.
        Assert.Equal(256, checks);
    }
}
