namespace Centerly.Core.Geometry;

/// <summary>Integer rectangle in physical screen pixels, edges are exclusive on the right/bottom like Win32 RECT.</summary>
public readonly record struct RectI(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;

    public int Height => Bottom - Top;

    public static RectI FromSize(int left, int top, int width, int height) =>
        new(left, top, left + width, top + height);

    public override string ToString() => $"({Left},{Top}) {Width}x{Height}";
}
