using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Centerly.Core.Geometry;
using Centerly.Core.Interop;

namespace Centerly.App.Views;

/// <summary>Click-through topmost frame drawn around the window under the crosshair, with the app name.</summary>
internal sealed class HighlightOverlay : Window
{
    private readonly TextBlock _label;
    private nint _handle;

    public HighlightOverlay()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        Focusable = false;
        IsHitTestVisible = false;
        // Off-screen until positioned: the first Show() would otherwise flash at the default location.
        Left = -32000;
        Top = -32000;
        Width = 1;
        Height = 1;

        var accent = SystemParameters.WindowGlassBrush as SolidColorBrush ?? new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6));
        _label = new TextBlock
        {
            Foreground = Brushes.White,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        Content = new Border
        {
            BorderBrush = accent,
            BorderThickness = new Thickness(4),
            CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(Color.FromArgb(0x22, accent.Color.R, accent.Color.G, accent.Color.B)),
            Child = new Border
            {
                Background = accent,
                CornerRadius = new CornerRadius(0, 0, 6, 0),
                Padding = new Thickness(10, 4, 10, 6),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Child = _label,
            },
        };
    }

    /// <summary>Shows the frame over <paramref name="bounds"/> (physical pixels).</summary>
    public void ShowAt(RectI bounds, string label)
    {
        _label.Text = label;
        if (_handle == 0)
        {
            // Create the HWND without showing it, then position it in physical pixels ourselves:
            // WPF Left/Top are in DIPs of an unknown monitor.
            _handle = new WindowInteropHelper(this).EnsureHandle();
            OverlayWindow.MakeClickThrough(_handle);
        }

        if (!IsVisible)
            Show();

        // Moving onto a monitor with another DPI makes WPF resize the window to its own suggestion;
        // the second call restores the exact bounds.
        OverlayWindow.Show(_handle, bounds);
        OverlayWindow.Show(_handle, bounds);
    }
}
