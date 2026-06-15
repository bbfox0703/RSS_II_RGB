using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using RSS_II_RGB.Core;

namespace RSS_II_RGB.App;

/// <summary>
/// Lets the user pick the rectangle of a GIF to show on the keyboard. The selection
/// box is locked to the keyboard's grid aspect (24:6 = 4:1) so the baked animation
/// is never distorted. After drawing, the box can be moved (drag its body) or
/// resized (drag a corner). The image sits inside a padded surface, so a drag can
/// start outside the picture to reach the very edges. Returns the chosen rectangle
/// in source-image pixels via <see cref="Window.ShowDialog{TResult}"/> (a
/// <see cref="PixelRect"/>, or null on cancel).
/// </summary>
public partial class GifCropWindow : Window
{
    // Grid aspect: width / height. 24 columns over 6 rows.
    private const double AspectW = CoreConstants.MatrixCols;
    private const double AspectH = CoreConstants.MatrixRows;
    private const double Pad = 36;        // padding so a drag can start outside the image
    private const double HandleSize = 10;
    private const double HandleHit = 16;  // grab radius around a corner
    private const double MinBox = 8;      // ignore drags smaller than this (keeps the box on a stray click)

    private enum Mode { None, Draw, Move, Resize }

    private double _scale = 1;             // display px per source px
    private int _srcWidth;
    private int _srcHeight;
    private double _dispWidth;
    private double _dispHeight;

    private Mode _mode;
    private Point _anchor;                 // display coords (image-relative): fixed corner for draw/resize
    private double _moveOffX, _moveOffY;
    private double _cropX, _cropY, _cropW, _cropH;

    private readonly Cursor _curCross = new(StandardCursorType.Cross);
    private readonly Cursor _curMove = new(StandardCursorType.SizeAll);
    private readonly Cursor _curNwse = new(StandardCursorType.TopLeftCorner);
    private readonly Cursor _curNesw = new(StandardCursorType.TopRightCorner);

    public GifCropWindow() => InitializeComponent();

    public GifCropWindow(Bitmap image, PixelSize sourceSize, PixelRect? initialCrop) : this()
    {
        _srcWidth = Math.Max(1, sourceSize.Width);
        _srcHeight = Math.Max(1, sourceSize.Height);

        // Fit the preview into a comfortable box (upscaling tiny GIFs is fine here).
        const double maxW = 760, maxH = 520;
        _scale = Math.Min(maxW / _srcWidth, maxH / _srcHeight);
        _dispWidth = _srcWidth * _scale;
        _dispHeight = _srcHeight * _scale;

        CropSurface.Width = _dispWidth + 2 * Pad;
        CropSurface.Height = _dispHeight + 2 * Pad;
        Canvas.SetLeft(PreviewImage, Pad);
        Canvas.SetTop(PreviewImage, Pad);
        PreviewImage.Width = _dispWidth;
        PreviewImage.Height = _dispHeight;
        PreviewImage.Source = image;

        CropSurface.Cursor = _curCross;

        PixelRect rect = initialCrop is { } c && IsWithinSource(c) ? c : DefaultCrop();
        SetCropDisplay(rect.X * _scale, rect.Y * _scale, rect.Width * _scale, rect.Height * _scale);
    }

    private bool IsWithinSource(PixelRect r) =>
        r.Width > 0 && r.Height > 0 &&
        r.X >= 0 && r.Y >= 0 && r.X + r.Width <= _srcWidth && r.Y + r.Height <= _srcHeight;

    // Largest 4:1 rectangle that fits the source, centred.
    private PixelRect DefaultCrop()
    {
        int w = _srcWidth;
        int h = (int)Math.Round(_srcWidth * AspectH / AspectW);
        if (h > _srcHeight)
        {
            h = _srcHeight;
            w = (int)Math.Round(_srcHeight * AspectW / AspectH);
        }
        return new PixelRect((_srcWidth - w) / 2, (_srcHeight - h) / 2, w, h);
    }

    // ----- Pointer interaction -----

    private void OnSurfacePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        Point p = ToImage(e);
        int corner = CornerAt(p);
        if (corner >= 0)
        {
            _mode = Mode.Resize;
            _anchor = OppositeCorner(corner);
        }
        else if (InsideCrop(p))
        {
            _mode = Mode.Move;
            _moveOffX = p.X - _cropX;
            _moveOffY = p.Y - _cropY;
        }
        else
        {
            _mode = Mode.Draw;
            _anchor = Clamp(p);
        }
        e.Pointer.Capture(CropSurface);
    }

    private void OnSurfacePointerMoved(object? sender, PointerEventArgs e)
    {
        Point p = ToImage(e);
        switch (_mode)
        {
            case Mode.Draw:
            case Mode.Resize:
                if (BuildFromAnchor(_anchor, Clamp(p)) is { } r)
                {
                    SetCropDisplay(r.X, r.Y, r.W, r.H);
                }
                break;
            case Mode.Move:
                double nx = Math.Clamp(p.X - _moveOffX, 0, Math.Max(0, _dispWidth - _cropW));
                double ny = Math.Clamp(p.Y - _moveOffY, 0, Math.Max(0, _dispHeight - _cropH));
                SetCropDisplay(nx, ny, _cropW, _cropH);
                break;
            default:
                UpdateHoverCursor(p);
                break;
        }
    }

    private void OnSurfacePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_mode != Mode.None)
        {
            _mode = Mode.None;
            e.Pointer.Capture(null);
        }
    }

    // Build a 4:1 rectangle from a fixed anchor corner toward the cursor, clamped
    // inside the image so the aspect ratio is always preserved. Null if too small.
    private (double X, double Y, double W, double H)? BuildFromAnchor(Point anchor, Point cur)
    {
        double dx = cur.X - anchor.X;
        double dy = cur.Y - anchor.Y;

        double maxW = dx >= 0 ? _dispWidth - anchor.X : anchor.X;
        double maxH = dy >= 0 ? _dispHeight - anchor.Y : anchor.Y;

        double w = Math.Min(Math.Abs(dx), maxW);
        double h = w * AspectH / AspectW;
        if (h > maxH)
        {
            h = maxH;
            w = h * AspectW / AspectH;
        }
        if (w < MinBox)
        {
            return null;
        }

        double x = dx >= 0 ? anchor.X : anchor.X - w;
        double y = dy >= 0 ? anchor.Y : anchor.Y - h;
        return (x, y, w, h);
    }

    private void SetCropDisplay(double x, double y, double w, double h)
    {
        _cropX = x;
        _cropY = y;
        _cropW = w;
        _cropH = h;

        Canvas.SetLeft(CropRect, Pad + x);
        Canvas.SetTop(CropRect, Pad + y);
        CropRect.Width = w;
        CropRect.Height = h;

        PlaceHandle(HandleTL, x, y);
        PlaceHandle(HandleTR, x + w, y);
        PlaceHandle(HandleBL, x, y + h);
        PlaceHandle(HandleBR, x + w, y + h);
    }

    private static void PlaceHandle(Rectangle handle, double cx, double cy)
    {
        Canvas.SetLeft(handle, Pad + cx - HandleSize / 2);
        Canvas.SetTop(handle, Pad + cy - HandleSize / 2);
    }

    // Corner index: 0 = TL, 1 = TR, 2 = BL, 3 = BR; -1 if none is near.
    private int CornerAt(Point p)
    {
        ReadOnlySpan<(double X, double Y)> corners =
        [
            (_cropX, _cropY), (_cropX + _cropW, _cropY),
            (_cropX, _cropY + _cropH), (_cropX + _cropW, _cropY + _cropH),
        ];
        for (int i = 0; i < corners.Length; i++)
        {
            if (Math.Abs(p.X - corners[i].X) <= HandleHit && Math.Abs(p.Y - corners[i].Y) <= HandleHit)
            {
                return i;
            }
        }
        return -1;
    }

    private Point OppositeCorner(int corner) => corner switch
    {
        0 => new Point(_cropX + _cropW, _cropY + _cropH), // TL -> BR
        1 => new Point(_cropX, _cropY + _cropH),          // TR -> BL
        2 => new Point(_cropX + _cropW, _cropY),          // BL -> TR
        _ => new Point(_cropX, _cropY),                   // BR -> TL
    };

    private bool InsideCrop(Point p) =>
        p.X >= _cropX && p.X <= _cropX + _cropW && p.Y >= _cropY && p.Y <= _cropY + _cropH;

    private void UpdateHoverCursor(Point p)
    {
        int corner = CornerAt(p);
        CropSurface.Cursor = corner switch
        {
            0 or 3 => _curNwse,
            1 or 2 => _curNesw,
            _ => InsideCrop(p) ? _curMove : _curCross,
        };
    }

    private Point ToImage(PointerEventArgs e)
    {
        Point p = e.GetPosition(CropSurface);
        return new Point(p.X - Pad, p.Y - Pad);
    }

    private Point Clamp(Point p) => new(
        Math.Clamp(p.X, 0, _dispWidth),
        Math.Clamp(p.Y, 0, _dispHeight));

    private void OnOk(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        int x = (int)Math.Round(_cropX / _scale);
        int y = (int)Math.Round(_cropY / _scale);
        int w = (int)Math.Round(_cropW / _scale);
        int h = (int)Math.Round(_cropH / _scale);

        x = Math.Clamp(x, 0, _srcWidth - 1);
        y = Math.Clamp(y, 0, _srcHeight - 1);
        w = Math.Clamp(w, 1, _srcWidth - x);
        h = Math.Clamp(h, 1, _srcHeight - y);

        Close(new PixelRect(x, y, w, h));
    }

    private void OnCancel(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close(null);
}
