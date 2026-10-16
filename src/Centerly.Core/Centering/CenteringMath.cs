using Centerly.Core.Geometry;

namespace Centerly.Core.Centering;

public static class CenteringMath
{
    /// <summary>
    /// Computes the new window rectangle (in GetWindowRect coordinates) that visually centers the window in the work area.
    /// </summary>
    /// <param name="windowRect">Rectangle from GetWindowRect, includes the invisible resize borders.</param>
    /// <param name="visibleRect">Visible frame from DWMWA_EXTENDED_FRAME_BOUNDS.</param>
    /// <param name="workArea">Monitor work area (without the taskbar).</param>
    /// <param name="oversize">Behavior for windows larger than the work area.</param>
    public static RectI ComputeTarget(RectI windowRect, RectI visibleRect, RectI workArea, OversizeBehavior oversize)
    {
        // Invisible borders: window rect minus visible frame. DWM bounds may be bogus for some windows, never go negative.
        var borderLeft = Math.Max(0, visibleRect.Left - windowRect.Left);
        var borderTop = Math.Max(0, visibleRect.Top - windowRect.Top);
        var borderRight = Math.Max(0, windowRect.Right - visibleRect.Right);
        var borderBottom = Math.Max(0, windowRect.Bottom - visibleRect.Bottom);

        var width = visibleRect.Width;
        var height = visibleRect.Height;
        if (oversize == OversizeBehavior.ShrinkToFit)
        {
            width = Math.Min(width, workArea.Width);
            height = Math.Min(height, workArea.Height);
        }

        var x = CenterOnAxis(workArea.Left, workArea.Width, width);
        var y = CenterOnAxis(workArea.Top, workArea.Height, height);

        return new RectI(
            x - borderLeft,
            y - borderTop,
            x + width + borderRight,
            y + height + borderBottom);
    }

    private static int CenterOnAxis(int start, int available, int size) =>
        size >= available ? start : start + (available - size) / 2;
}
