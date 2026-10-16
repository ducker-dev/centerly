using Centerly.Core.Centering;
using Centerly.Core.Geometry;

namespace Centerly.Tests;

public class CenteringMathTests
{
    // 1920x1080 monitor with a 48px taskbar at the bottom.
    private static readonly RectI WorkArea = new(0, 0, 1920, 1032);

    [Fact]
    public void Centers_visible_frame_not_window_rect()
    {
        // Typical Win11 window: 7px invisible borders left/right/bottom, none on top.
        var visible = RectI.FromSize(100, 100, 800, 600);
        var window = new RectI(visible.Left - 7, visible.Top, visible.Right + 7, visible.Bottom + 7);

        var target = CenteringMath.ComputeTarget(window, visible, WorkArea, OversizeBehavior.KeepSize);

        // Visible frame must land at ((1920-800)/2, (1032-600)/2) = (560, 216).
        Assert.Equal(new RectI(560 - 7, 216, 560 + 800 + 7, 216 + 600 + 7), target);
    }

    [Fact]
    public void Respects_work_area_offset_on_secondary_monitor()
    {
        // Monitor to the left of the primary, taskbar on its top edge.
        var workArea = new RectI(-2560, 40, 0, 1440);
        var visible = RectI.FromSize(-2000, 500, 1000, 400);

        var target = CenteringMath.ComputeTarget(visible, visible, workArea, OversizeBehavior.KeepSize);

        Assert.Equal(RectI.FromSize(-2560 + 780, 40 + 500, 1000, 400), target);
    }

    [Fact]
    public void Already_centered_window_maps_to_itself()
    {
        var visible = RectI.FromSize(560, 216, 800, 600);

        var target = CenteringMath.ComputeTarget(visible, visible, WorkArea, OversizeBehavior.KeepSize);

        Assert.Equal(visible, target);
    }

    [Fact]
    public void Oversized_window_keeps_size_and_pins_to_top_left()
    {
        var visible = RectI.FromSize(300, 300, 2400, 1200);

        var target = CenteringMath.ComputeTarget(visible, visible, WorkArea, OversizeBehavior.KeepSize);

        Assert.Equal(RectI.FromSize(0, 0, 2400, 1200), target);
    }

    [Fact]
    public void Oversized_on_one_axis_still_centers_the_other()
    {
        var visible = RectI.FromSize(300, 300, 800, 1200);

        var target = CenteringMath.ComputeTarget(visible, visible, WorkArea, OversizeBehavior.KeepSize);

        Assert.Equal(RectI.FromSize(560, 0, 800, 1200), target);
    }

    [Fact]
    public void Shrink_to_fit_resizes_oversized_axis_only()
    {
        var visible = RectI.FromSize(300, 300, 800, 1200);
        var window = new RectI(visible.Left - 7, visible.Top, visible.Right + 7, visible.Bottom + 7);

        var target = CenteringMath.ComputeTarget(window, visible, WorkArea, OversizeBehavior.ShrinkToFit);

        Assert.Equal(new RectI(560 - 7, 0, 560 + 800 + 7, 1032 + 7), target);
    }

    [Fact]
    public void Bogus_frame_outside_window_rect_does_not_produce_negative_borders()
    {
        var window = RectI.FromSize(100, 100, 800, 600);
        var visible = RectI.FromSize(90, 90, 820, 620);

        var target = CenteringMath.ComputeTarget(window, visible, WorkArea, OversizeBehavior.KeepSize);

        Assert.Equal(RectI.FromSize(550, 206, 820, 620), target);
    }
}
