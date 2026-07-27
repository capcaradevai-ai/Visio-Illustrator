using VisioTestBridge.Contracts;
using VisioTestBridge.Core.Session;

namespace VisioTestBridge.Core.Geometry;

public sealed class GeometryConverter
{
    private readonly CanvasSpec _canvas;
    private readonly double _marginPx;

    public GeometryConverter(CanvasSpec canvas, double marginPx = 0)
    {
        if (canvas.WidthPx <= 0
            || canvas.HeightPx <= 0
            || canvas.Dpi is < 48 or > 600
            || !double.IsFinite(marginPx)
            || marginPx < 0)
        {
            throw Invalid("Canvas 尺寸、DPI 或 margin 无效。");
        }

        _canvas = canvas;
        _marginPx = marginPx;
    }

    public VisioBox ToVisio(PixelBox box)
    {
        ValidatePixelBox(box);
        return new VisioBox(
            (box.X + box.Width / 2d) / _canvas.Dpi,
            (_canvas.HeightPx - box.Y - box.Height / 2d) / _canvas.Dpi,
            box.Width / _canvas.Dpi,
            box.Height / _canvas.Dpi);
    }

    public PixelBox ToPixels(VisioBox box)
    {
        var result = ToPixelsAllowingOverflow(box);
        ValidatePixelBox(result);
        return result;
    }

    public PixelBox ToPixelsAllowingOverflow(VisioBox box)
    {
        if (!AreFinite(
                box.PinXInches,
                box.PinYInches,
                box.WidthInches,
                box.HeightInches)
            || box.WidthInches <= 0
            || box.HeightInches <= 0)
        {
            throw Invalid("Visio 几何包含非法数值或非正尺寸。");
        }

        var widthPx = box.WidthInches * _canvas.Dpi;
        var heightPx = box.HeightInches * _canvas.Dpi;
        var result = new PixelBox(
            box.PinXInches * _canvas.Dpi - widthPx / 2d,
            _canvas.HeightPx - box.PinYInches * _canvas.Dpi - heightPx / 2d,
            widthPx,
            heightPx);
        return result;
    }

    public double PixelsToInches(double pixels)
    {
        if (!double.IsFinite(pixels)) throw Invalid("像素距离必须是有限数值。");
        return pixels / _canvas.Dpi;
    }

    public (double X, double Y) ToVisioPoint(PixelPoint point)
    {
        if (!AreFinite(point.X, point.Y)
            || point.X < -_marginPx
            || point.Y < -_marginPx
            || point.X > _canvas.WidthPx + _marginPx
            || point.Y > _canvas.HeightPx + _marginPx)
        {
            throw Invalid("连接线路径点超出允许的画布边界。");
        }
        return (
            point.X / _canvas.Dpi,
            (_canvas.HeightPx - point.Y) / _canvas.Dpi);
    }

    private void ValidatePixelBox(PixelBox box)
    {
        if (!AreFinite(box.X, box.Y, box.Width, box.Height)
            || box.Width <= 0
            || box.Height <= 0)
        {
            throw Invalid("像素几何包含非法数值或非正尺寸。");
        }

        if (box.X < -_marginPx
            || box.Y < -_marginPx
            || box.X + box.Width > _canvas.WidthPx + _marginPx
            || box.Y + box.Height > _canvas.HeightPx + _marginPx)
        {
            throw Invalid("像素几何超出允许的画布边界。");
        }
    }

    private static bool AreFinite(params double[] values) => values.All(double.IsFinite);

    private static BridgeCommandException Invalid(string message) =>
        new(BridgeErrorCodes.InvalidCoordinates, message);
}
