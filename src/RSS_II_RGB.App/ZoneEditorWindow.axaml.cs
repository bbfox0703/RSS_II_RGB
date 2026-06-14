using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace RSS_II_RGB.App;

public partial class ZoneEditorWindow : Window
{
    private const double KeySize = 28;

    private bool _dragging;
    private Point _dragStart;

    public ZoneEditorWindow() => InitializeComponent();

    private void OnKeyboardPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // A press on a key toggles it (the ToggleButton handles that); only empty
        // space starts a rubber-band selection.
        if (IsOnKey(e.Source))
        {
            return;
        }

        _dragStart = e.GetPosition(KeyboardHost);
        _dragging = true;
        e.Pointer.Capture(KeyboardHost);
        UpdateSelectionRect(_dragStart, _dragStart);
        SelectionRect.IsVisible = true;
    }

    private void OnKeyboardPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_dragging)
        {
            UpdateSelectionRect(_dragStart, e.GetPosition(KeyboardHost));
        }
    }

    private void OnKeyboardPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        _dragging = false;
        e.Pointer.Capture(null);
        SelectionRect.IsVisible = false;
        SelectKeysInRect(_dragStart, e.GetPosition(KeyboardHost));
    }

    private void UpdateSelectionRect(Point a, Point b)
    {
        double x = Math.Min(a.X, b.X);
        double y = Math.Min(a.Y, b.Y);
        SelectionRect.Margin = new Thickness(x, y, 0, 0);
        SelectionRect.Width = Math.Abs(a.X - b.X);
        SelectionRect.Height = Math.Abs(a.Y - b.Y);
    }

    private void SelectKeysInRect(Point a, Point b)
    {
        if (DataContext is not ZoneEditorViewModel vm)
        {
            return;
        }

        double minX = Math.Min(a.X, b.X), maxX = Math.Max(a.X, b.X);
        double minY = Math.Min(a.Y, b.Y), maxY = Math.Max(a.Y, b.Y);

        // Ignore a stray click (no real drag).
        if (maxX - minX < 3 && maxY - minY < 3)
        {
            return;
        }

        // Key positions are inset from the host origin by EdgePad.
        const double pad = ZoneEditorViewModel.EdgePad;
        foreach (KeyVM key in vm.Keys)
        {
            double kx = key.X + pad, ky = key.Y + pad;
            bool intersects = !(kx + KeySize < minX || kx > maxX ||
                                ky + KeySize < minY || ky > maxY);
            if (intersects)
            {
                key.IsSelected = true;
            }
        }
    }

    private static bool IsOnKey(object? source)
    {
        Visual? visual = source as Visual;
        while (visual is not null)
        {
            if (visual is ToggleButton)
            {
                return true;
            }
            visual = visual.GetVisualParent();
        }
        return false;
    }
}
