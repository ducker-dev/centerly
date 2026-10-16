using Centerly.Core.Geometry;

namespace Centerly.Tests;

public class MonitorMathTests
{
    private static MonitorArea Monitor(int left, int top, int width, int height) =>
        new(RectI.FromSize(left, top, width, height), RectI.FromSize(left, top, width, height - 48));

    private static readonly MonitorArea Left = Monitor(-1920, 0, 1920, 1080);
    private static readonly MonitorArea Primary = Monitor(0, 0, 3840, 2160);
    private static readonly MonitorArea Right = Monitor(3840, 200, 2560, 1440);

    [Fact]
    public void Goes_left_to_right_regardless_of_enumeration_order()
    {
        MonitorArea[] monitors = [Primary, Right, Left];

        Assert.Equal(Primary, MonitorMath.Next(monitors, Left));
        Assert.Equal(Right, MonitorMath.Next(monitors, Primary));
    }

    [Fact]
    public void Wraps_around_from_last_to_first() =>
        Assert.Equal(Left, MonitorMath.Next([Left, Primary, Right], Right));

    [Fact]
    public void Single_monitor_returns_itself() =>
        Assert.Equal(Primary, MonitorMath.Next([Primary], Primary));

    [Fact]
    public void Unknown_current_monitor_returns_itself() =>
        Assert.Equal(Right, MonitorMath.Next([Left, Primary], Right));

    [Fact]
    public void Previous_goes_right_to_left_and_wraps()
    {
        MonitorArea[] monitors = [Primary, Right, Left];

        Assert.Equal(Primary, MonitorMath.Previous(monitors, Right));
        Assert.Equal(Left, MonitorMath.Previous(monitors, Primary));
        Assert.Equal(Right, MonitorMath.Previous(monitors, Left));
    }

    [Fact]
    public void Previous_on_single_or_unknown_monitor_returns_itself()
    {
        Assert.Equal(Primary, MonitorMath.Previous([Primary], Primary));
        Assert.Equal(Right, MonitorMath.Previous([Left, Primary], Right));
    }

    [Fact]
    public void Containing_finds_monitor_by_rect_center()
    {
        MonitorArea[] monitors = [Left, Primary, Right];

        Assert.Equal(Right, MonitorMath.Containing(monitors, RectI.FromSize(4000, 500, 800, 600)));
        // A maximized window overhangs its monitor by its invisible borders; the center still decides.
        Assert.Equal(Left, MonitorMath.Containing(monitors, new RectI(-1931, -11, 11, 1043)));
    }

    [Fact]
    public void Containing_falls_back_to_nearest_monitor()
    {
        // Saved on a monitor that has been disconnected since: far to the right of everything.
        Assert.Equal(Right, MonitorMath.Containing([Left, Primary, Right], RectI.FromSize(9000, 500, 800, 600)));
        Assert.Null(MonitorMath.Containing([], RectI.FromSize(0, 0, 10, 10)));
    }

    [Fact]
    public void Stacked_monitors_go_top_to_bottom()
    {
        var top = Monitor(0, -1080, 1920, 1080);
        var bottom = Monitor(0, 0, 1920, 1080);

        Assert.Equal(bottom, MonitorMath.Next([bottom, top], top));
        Assert.Equal(top, MonitorMath.Next([bottom, top], bottom));
    }
}
