using System.Windows;
using System.Windows.Input;
using Centerly.App.Views;
using Centerly.Core.AutoCenter;

namespace Centerly.App.Controls;

/// <summary>
/// "Crosshair" picking: press the mouse on the element, drag over any window (it gets highlighted), release to pick it.
/// Esc or losing the mouse capture cancels.
/// </summary>
internal sealed class WindowPickerBehavior
{
    private readonly UIElement _element;
    private readonly Action<PickedWindow> _onPicked;
    private readonly Action _onClickedWithoutDrag;
    private readonly Func<PickedWindow, string> _describe;

    private HighlightOverlay? _overlay;
    private PickedWindow? _current;
    private bool _isDragging;

    private WindowPickerBehavior(UIElement element, Action<PickedWindow> onPicked, Action onClickedWithoutDrag, Func<PickedWindow, string> describe)
    {
        _element = element;
        _onPicked = onPicked;
        _onClickedWithoutDrag = onClickedWithoutDrag;
        _describe = describe;

        element.PreviewMouseLeftButtonDown += OnMouseDown;
        element.PreviewMouseMove += OnMouseMove;
        element.PreviewMouseLeftButtonUp += OnMouseUp;
        element.LostMouseCapture += (_, _) => Finish(pick: false);
        element.PreviewKeyDown += (_, e) =>
        {
            if (_isDragging && e.Key == Key.Escape)
            {
                e.Handled = true;
                Finish(pick: false);
            }
        };
    }

    /// <param name="element">Element the user presses (a button with a crosshair icon).</param>
    /// <param name="onPicked">Called with the window under the cursor on release.</param>
    /// <param name="onClickedWithoutDrag">Called when the user just clicks: explain that dragging is needed.</param>
    /// <param name="describe">Label shown on the highlight.</param>
    public static void Attach(UIElement element, Action<PickedWindow> onPicked, Action onClickedWithoutDrag, Func<PickedWindow, string> describe) =>
        _ = new WindowPickerBehavior(element, onPicked, onClickedWithoutDrag, describe);

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        _isDragging = true;
        _current = null;
        _element.CaptureMouse();
        Mouse.OverrideCursor = Cursors.Cross;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging)
            return;

        _current = WindowPicker.AtCursor();
        if (_current is null)
        {
            _overlay?.Hide();
            return;
        }

        _overlay ??= new HighlightOverlay();
        _overlay.ShowAt(_current.VisibleRect, _describe(_current));
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging)
            return;

        e.Handled = true;
        _current = WindowPicker.AtCursor();
        Finish(pick: true);
    }

    private void Finish(bool pick)
    {
        if (!_isDragging)
            return;

        _isDragging = false;
        Mouse.OverrideCursor = null;
        _overlay?.Hide();
        if (_element.IsMouseCaptured)
            _element.ReleaseMouseCapture();

        if (!pick)
            return;

        if (_current is { } picked)
            _onPicked(picked);
        else if (_element.IsMouseOver)
            _onClickedWithoutDrag();
    }
}
