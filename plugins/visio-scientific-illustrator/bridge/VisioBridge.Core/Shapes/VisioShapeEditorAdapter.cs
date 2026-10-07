using System.Runtime.InteropServices;
using VisioBridge.Contracts;
using VisioBridge.Core.Abstractions;
using VisioBridge.Core.Geometry;
using VisioBridge.Core.Inspection;
using VisioBridge.Core.Text;
using Visio = Microsoft.Office.Interop.Visio;

namespace VisioBridge.Core.Shapes;

internal sealed class VisioShapeEditorAdapter : IShapeEditorAdapter
{
    private readonly Visio.Page _page;
    private readonly ShapeRepository _repository;
    private readonly GeometryConverter _geometry;
    private readonly PageInspector _inspector;

    public VisioShapeEditorAdapter(
        Visio.Page page,
        ShapeRepository repository,
        GeometryConverter geometry,
        PageInspector inspector)
    {
        _page = page;
        _repository = repository;
        _geometry = geometry;
        _inspector = inspector;
    }

    public ShapeEditState Read(string semanticId)
    {
        using var handle = _repository.Resolve(semanticId);
        return new ShapeEditState(
            _inspector.ReadBounds(((VisioShapeHandle)handle).Shape));
    }

    public ShapeSnapshot Apply(UpdateShapeCommand command, PixelBox? mergedBounds)
    {
        using var handle = _repository.Resolve(command.SemanticId);
        var shape = ((VisioShapeHandle)handle).Shape;
        if (command.Label is not null && command.RichText is not null)
            throw new BridgeCommandException(BridgeErrorCodes.InvalidArgument, "label 与 rich_text 不能同时提供。");
        if (command.RichText is not null) RichTextFormatter.Apply(shape, command.RichText);
        else if (command.Label is not null) shape.Text = command.Label;
        StyleMapper.Apply(shape, command.Style);
        if (mergedBounds is not null)
        {
            ApplyPageBounds(shape, command, mergedBounds);
        }
        return _inspector.Snapshot(shape);
    }

    private void ApplyPageBounds(
        Visio.Shape shape,
        UpdateShapeCommand command,
        PixelBox mergedBounds)
    {
        if (VisioShapeClassifier.IsConnector(shape))
        {
            throw BridgeCommandException.Unsupported(
                "不能通过二维 bounds 修改 1D connector。");
        }

        var current = _inspector.ReadBounds(shape);
        var desired = new PixelBox(
            command.X ?? current.X,
            command.Y ?? current.Y,
            command.Width ?? current.Width,
            command.Height ?? current.Height);
        _geometry.ToVisio(desired);

        if (command.Width is not null || command.Height is not null)
        {
            ResizeToPageBounds(shape, current, desired);
            current = _inspector.ReadBounds(shape);
        }

        TranslateInPageCoordinates(
            shape,
            _geometry.PixelsToInches(desired.X - current.X),
            -_geometry.PixelsToInches(desired.Y - current.Y));

        var actual = _inspector.ReadBounds(shape);
        VerifyPageBounds(actual, desired);
    }

    private void ResizeToPageBounds(
        Visio.Shape shape,
        PixelBox current,
        PixelBox desired)
    {
        const double determinantTolerance = 1e-9;
        var desiredWidth = _geometry.PixelsToInches(desired.Width);
        var desiredHeight = _geometry.PixelsToInches(desired.Height);
        var (xx, xy, yx, yy) = ReadPageTransform(shape);
        var a = Math.Abs(xx);
        var b = Math.Abs(yx);
        var c = Math.Abs(xy);
        var d = Math.Abs(yy);
        var determinant = a * d - b * c;
        var currentWidth = ReadResultIU(shape, "Width");
        var currentHeight = ReadResultIU(shape, "Height");
        double localWidth;
        double localHeight;

        if (Math.Abs(determinant) < determinantTolerance)
        {
            var widthScale = desired.Width / current.Width;
            var heightScale = desired.Height / current.Height;
            if (!double.IsFinite(widthScale)
                || !double.IsFinite(heightScale)
                || Math.Abs(widthScale - heightScale) > 1e-6)
            {
                throw BridgeCommandException.Unsupported(
                    "当前旋转或分组变换无法在保持未请求外接框字段时独立调整宽高。");
            }
            localWidth = currentWidth * widthScale;
            localHeight = currentHeight * heightScale;
        }
        else
        {
            localWidth = (desiredWidth * d - b * desiredHeight) / determinant;
            localHeight = (a * desiredHeight - c * desiredWidth) / determinant;
        }

        if (!double.IsFinite(localWidth)
            || !double.IsFinite(localHeight)
            || localWidth <= 0
            || localHeight <= 0)
        {
            throw BridgeCommandException.Unsupported(
                "请求的页面外接框无法映射为正的 Visio 本地宽高。");
        }

        StyleMapper.SetResultInches(shape, "Width", localWidth);
        StyleMapper.SetResultInches(shape, "Height", localHeight);
    }

    private void TranslateInPageCoordinates(
        Visio.Shape shape,
        double pageDeltaX,
        double pageDeltaY)
    {
        var localDeltaX = pageDeltaX;
        var localDeltaY = pageDeltaY;
        Visio.Shape? containingShape = null;
        try
        {
            containingShape = shape.ContainingShape;
            if (containingShape is not null)
            {
                containingShape.TransformXYFrom(
                    null!,
                    0,
                    0,
                    out var localOriginX,
                    out var localOriginY);
                containingShape.TransformXYFrom(
                    null!,
                    pageDeltaX,
                    pageDeltaY,
                    out var localTargetX,
                    out var localTargetY);
                localDeltaX = localTargetX - localOriginX;
                localDeltaY = localTargetY - localOriginY;
            }
        }
        finally
        {
            if (containingShape is not null) Marshal.ReleaseComObject(containingShape);
        }

        StyleMapper.SetResultInches(
            shape,
            "PinX",
            ReadResultIU(shape, "PinX") + localDeltaX);
        StyleMapper.SetResultInches(
            shape,
            "PinY",
            ReadResultIU(shape, "PinY") + localDeltaY);
    }

    private static (double Xx, double Xy, double Yx, double Yy) ReadPageTransform(
        Visio.Shape shape)
    {
        shape.XYToPage(0, 0, out var originX, out var originY);
        shape.XYToPage(1, 0, out var xX, out var xY);
        shape.XYToPage(0, 1, out var yX, out var yY);
        return (xX - originX, xY - originY, yX - originX, yY - originY);
    }

    private static double ReadResultIU(Visio.Shape shape, string cellName)
    {
        if (shape.CellExistsU[
                cellName,
                (short)Visio.VisExistsFlags.visExistsAnywhere] == 0)
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.CellNotFound,
                $"Shape 缺少必需单元格: {cellName}");
        }
        var cell = shape.CellsU[cellName];
        try
        {
            return cell.ResultIU;
        }
        finally
        {
            Marshal.ReleaseComObject(cell);
        }
    }

    private static void VerifyPageBounds(PixelBox actual, PixelBox desired)
    {
        const double tolerancePx = 0.75;
        if (Math.Abs(actual.X - desired.X) > tolerancePx
            || Math.Abs(actual.Y - desired.Y) > tolerancePx
            || Math.Abs(actual.Width - desired.Width) > tolerancePx
            || Math.Abs(actual.Height - desired.Height) > tolerancePx)
        {
            throw BridgeCommandException.Unsupported(
                "Visio 无法在保持页面坐标语义的前提下应用请求的外接框。");
        }
    }

    public DeleteResult Delete(string semanticId, bool deleteConnectors)
    {
        using var handle = _repository.Resolve(semanticId);
        var shape = ((VisioShapeHandle)handle).Shape;
        var connectorIds = FindAffectedConnectorIds(shape.ID);
        if (deleteConnectors)
        {
            foreach (var connectorId in connectorIds) DeleteShapeById(connectorId);
        }
        shape.Delete();
        return new DeleteResult(
            semanticId,
            deleteConnectors ? connectorIds.Count : 0);
    }

    public int Clear()
    {
        var shapes = _page.Shapes;
        try
        {
            var count = shapes.Count;
            for (var index = count; index >= 1; index--)
            {
                var shape = shapes[index];
                try
                {
                    shape.Delete();
                }
                finally
                {
                    Marshal.ReleaseComObject(shape);
                }
            }
            return count;
        }
        finally
        {
            Marshal.ReleaseComObject(shapes);
        }
    }

    private HashSet<int> FindAffectedConnectorIds(int targetId)
    {
        var result = new HashSet<int>();
        var shapes = _page.Shapes;
        try
        {
            for (var index = 1; index <= shapes.Count; index++)
            {
                var candidate = shapes[index];
                try
                {
                    if (!VisioShapeClassifier.IsConnector(candidate)) continue;
                    var connects = candidate.Connects;
                    try
                    {
                        for (var connectIndex = 1; connectIndex <= connects.Count; connectIndex++)
                        {
                            var connect = connects[connectIndex];
                            Visio.Shape? target = null;
                            try
                            {
                                target = connect.ToSheet;
                                if (target.ID == targetId) result.Add(candidate.ID);
                            }
                            finally
                            {
                                if (target is not null) Marshal.ReleaseComObject(target);
                                Marshal.ReleaseComObject(connect);
                            }
                        }
                    }
                    finally
                    {
                        Marshal.ReleaseComObject(connects);
                    }
                }
                finally
                {
                    Marshal.ReleaseComObject(candidate);
                }
            }
        }
        finally
        {
            Marshal.ReleaseComObject(shapes);
        }
        return result;
    }

    private void DeleteShapeById(int shapeId)
    {
        var shapes = _page.Shapes;
        Visio.Shape? shape = null;
        try
        {
            shape = shapes.ItemFromID[shapeId];
            shape.Delete();
        }
        finally
        {
            if (shape is not null) Marshal.ReleaseComObject(shape);
            Marshal.ReleaseComObject(shapes);
        }
    }
}
