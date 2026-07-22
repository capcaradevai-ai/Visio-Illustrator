using System.Runtime.InteropServices;
using VisioBridge.Contracts;
using VisioBridge.Core.Abstractions;
using VisioBridge.Core.Geometry;
using VisioBridge.Core.Interop;
using Visio = Microsoft.Office.Interop.Visio;

namespace VisioBridge.Core.Shapes;

internal sealed class VisioConnectorAdapter : IConnectorAdapter
{
    private static readonly IReadOnlyDictionary<string, string> ArrowFormulas =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["none"] = "0",
            ["triangle"] = "4",
            ["open"] = "5",
        };

    private readonly Visio.Page _page;
    private readonly Visio.InvisibleApp _application;
    private readonly GeometryConverter _geometry;

    public VisioConnectorAdapter(
        Visio.Page page,
        Visio.InvisibleApp application,
        GeometryConverter geometry)
    {
        _page = page;
        _application = application;
        _geometry = geometry;
    }

    public IShapeHandle Create(
        IShapeHandle source,
        IShapeHandle target,
        string routing,
        IReadOnlyList<PixelPoint>? waypoints)
    {
        var sourceShape = RequireVisio(source);
        var targetShape = RequireVisio(target);
        var sourcePoint = ReadPagePin(sourceShape.Shape, "read_source_pin");
        var targetPoint = ReadPagePin(targetShape.Shape, "read_target_pin");
        if (routing == "polyline")
        {
            var values = new List<double> { sourcePoint.X, sourcePoint.Y };
            foreach (var waypoint in waypoints!)
            {
                var point = _geometry.ToVisioPoint(waypoint);
                values.Add(point.X);
                values.Add(point.Y);
            }
            values.Add(targetPoint.X);
            values.Add(targetPoint.Y);
            Array points = values.ToArray();
            Visio.Shape? polyline = null;
            try
            {
                try
                {
                    polyline = _page.DrawPolyline(
                        ref points,
                        (short)Visio.VisDrawSplineFlags.visPolyline1D);
                }
                catch (COMException error)
                {
                    throw ConnectorComError(
                        error,
                        "draw_polyline",
                        BridgeErrorCodes.InvalidCoordinates);
                }
                StyleMapper.SetFormula(polyline, "ObjType", "2");
                StyleMapper.SetFormula(polyline, "GlueType", "2");
                var handle = new VisioShapeHandle(polyline);
                polyline = null;
                return handle;
            }
            finally
            {
                Release(ref polyline);
            }
        }

        object? connectorData = null;
        Visio.Shape? connector = null;
        try
        {
            connectorData = _application.ConnectorToolDataObject;
            connector = _page.Drop(
                connectorData,
                (sourcePoint.X + targetPoint.X) / 2,
                (sourcePoint.Y + targetPoint.Y) / 2);
            StyleMapper.SetFormula(
                connector,
                "ShapeRouteStyle",
                routing switch
                {
                    "dynamic" => "0",
                    "right_angle" => "1",
                    "straight" => "2",
                    _ => throw new BridgeCommandException(
                        BridgeErrorCodes.InvalidArgument,
                        $"不支持 connector routing: {routing}"),
                });
            var handle = new VisioShapeHandle(connector);
            connector = null;
            return handle;
        }
        finally
        {
            Release(ref connector);
            if (connectorData is not null && Marshal.IsComObject(connectorData))
            {
                Marshal.ReleaseComObject(connectorData);
            }
        }
    }

    public void Glue(
        IShapeHandle connector,
        IShapeHandle source,
        IShapeHandle target,
        int? sourceConnectionPoint,
        int? targetConnectionPoint)
    {
        var connectorShape = RequireVisio(connector).Shape;
        var sourceShape = RequireVisio(source).Shape;
        var targetShape = RequireVisio(target).Shape;
        Visio.Cell? begin = null;
        Visio.Cell? end = null;
        Visio.Cell? sourcePin = null;
        Visio.Cell? targetPin = null;
        try
        {
            begin = connectorShape.CellsU["BeginX"];
            end = connectorShape.CellsU["EndX"];
            sourcePin = GetConnectionCell(sourceShape, sourceConnectionPoint);
            targetPin = GetConnectionCell(targetShape, targetConnectionPoint);
            begin.GlueTo(sourcePin);
            end.GlueTo(targetPin);
        }
        finally
        {
            Release(ref targetPin);
            Release(ref sourcePin);
            Release(ref end);
            Release(ref begin);
        }
    }

    public void SetLabel(IShapeHandle connector, string? label)
    {
        if (label is not null) RequireVisio(connector).Shape.Text = label;
    }

    public void ApplyStyle(IShapeHandle connector, ConnectorStyle? style)
    {
        if (style is null) return;
        var shape = RequireVisio(connector).Shape;
        if (style.StrokeColor is not null)
        {
            StyleMapper.SetFormula(shape, "LineColor", StyleMapper.ToRgbFormula(style.StrokeColor));
        }
        if (style.StrokeWidthPt is not null)
        {
            if (!double.IsFinite(style.StrokeWidthPt.Value)
                || style.StrokeWidthPt.Value is < 0 or > 50)
            {
                throw new BridgeCommandException(
                    BridgeErrorCodes.InvalidStyle,
                    "stroke_width_pt 必须在 0–50 范围内。");
            }
            StyleMapper.SetFormula(
                shape,
                "LineWeight",
                $"{style.StrokeWidthPt.Value.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture)} pt");
        }
        if (style.Dashed is not null)
        {
            StyleMapper.SetFormula(shape, "LinePattern", style.Dashed.Value ? "2" : "1");
        }
        ApplyArrow(shape, "BeginArrow", style.StartArrow);
        ApplyArrow(shape, "EndArrow", style.EndArrow);
    }

    public int CountVerifiedEndpointConnections(
        IShapeHandle connector,
        IShapeHandle source,
        IShapeHandle target)
    {
        var connectorShape = RequireVisio(connector).Shape;
        var beginMatched = false;
        var endMatched = false;
        var connects = connectorShape.Connects;
        try
        {
            for (var index = 1; index <= connects.Count; index++)
            {
                var connection = connects[index];
                Visio.Shape? toShape = null;
                try
                {
                    toShape = connection.ToSheet;
                    if (connection.FromPart is
                            (short)Visio.VisFromParts.visBeginX or
                            (short)Visio.VisFromParts.visBegin
                        && toShape.ID == source.Id)
                    {
                        beginMatched = true;
                    }
                    if (connection.FromPart is
                            (short)Visio.VisFromParts.visEndX or
                            (short)Visio.VisFromParts.visEnd
                        && toShape.ID == target.Id)
                    {
                        endMatched = true;
                    }
                }
                finally
                {
                    Release(ref toShape);
                    Marshal.ReleaseComObject(connection);
                }
            }
        }
        finally
        {
            Marshal.ReleaseComObject(connects);
        }
        return (beginMatched ? 1 : 0) + (endMatched ? 1 : 0);
    }

    public void Delete(IShapeHandle connector) => RequireVisio(connector).Shape.Delete();

    private static VisioShapeHandle RequireVisio(IShapeHandle handle) =>
        handle as VisioShapeHandle
        ?? throw new BridgeCommandException(
            BridgeErrorCodes.InternalError,
            "Connector adapter 收到非 Visio Shape handle。");

    private static Visio.Cell GetConnectionCell(Visio.Shape shape, int? row)
    {
        var cellName = row.HasValue ? $"Connections.X{row.Value}" : "PinX";
        if (shape.CellExistsU[cellName, 0] == 0)
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.CellNotFound,
                $"Shape {shape.NameU} 不存在连接点 {cellName}。");
        }
        return shape.CellsU[cellName];
    }

    private static (double X, double Y) ReadPagePin(
        Visio.Shape shape,
        string phase)
    {
        Visio.Cell? pinX = null;
        Visio.Cell? pinY = null;
        Visio.Shape? containingShape = null;
        try
        {
            pinX = shape.CellsU["PinX"];
            pinY = shape.CellsU["PinY"];
            var pageX = pinX.ResultIU;
            var pageY = pinY.ResultIU;
            containingShape = shape.ContainingShape;
            if (containingShape is not null)
            {
                containingShape.XYToPage(pageX, pageY, out pageX, out pageY);
            }
            return (pageX, pageY);
        }
        catch (COMException error)
        {
            throw ConnectorComError(
                error,
                phase,
                BridgeErrorCodes.InvalidCoordinates);
        }
        finally
        {
            Release(ref containingShape);
            Release(ref pinY);
            Release(ref pinX);
        }
    }

    private static BridgeCommandException ConnectorComError(
        COMException error,
        string phase,
        string code)
    {
        var translated = ComExceptionTranslator.Translate(error);
        return translated.Code == BridgeErrorCodes.VisioBusy
            ? translated
            : new BridgeCommandException(
                code,
                $"Visio connector 操作失败: {phase}。",
                details: new { phase, hresult = error.HResult, reason = error.Message });
    }

    private static void ApplyArrow(Visio.Shape shape, string cellName, string? arrow)
    {
        if (arrow is null) return;
        if (!ArrowFormulas.TryGetValue(arrow, out var formula))
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.InvalidStyle,
                $"不支持 arrow: {arrow}");
        }
        StyleMapper.SetFormula(shape, cellName, formula);
    }

    private static void Release<T>(ref T? value)
        where T : class
    {
        if (value is not null) Marshal.ReleaseComObject(value);
        value = null;
    }
}
