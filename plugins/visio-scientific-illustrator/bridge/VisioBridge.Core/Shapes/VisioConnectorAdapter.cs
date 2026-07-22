using System.Runtime.InteropServices;
using VisioBridge.Contracts;
using VisioBridge.Core.Abstractions;
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

    public VisioConnectorAdapter(Visio.Page page, Visio.InvisibleApp application)
    {
        _page = page;
        _application = application;
    }

    public IShapeHandle Create(IShapeHandle source, IShapeHandle target)
    {
        var sourceShape = RequireVisio(source);
        var targetShape = RequireVisio(target);
        var sourcePoint = ReadPin(sourceShape.Shape);
        var targetPoint = ReadPin(targetShape.Shape);
        object? connectorData = null;
        try
        {
            connectorData = _application.ConnectorToolDataObject;
            var connector = _page.Drop(
                connectorData,
                (sourcePoint.X + targetPoint.X) / 2,
                (sourcePoint.Y + targetPoint.Y) / 2);
            return new VisioShapeHandle(connector);
        }
        finally
        {
            if (connectorData is not null && Marshal.IsComObject(connectorData))
            {
                Marshal.ReleaseComObject(connectorData);
            }
        }
    }

    public void Glue(IShapeHandle connector, IShapeHandle source, IShapeHandle target)
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
            sourcePin = sourceShape.CellsU["PinX"];
            targetPin = targetShape.CellsU["PinX"];
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
        var expected = new HashSet<int> { source.Id, target.Id };
        var matched = new HashSet<int>();
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
                    if (expected.Contains(toShape.ID)) matched.Add(toShape.ID);
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
        return matched.Count;
    }

    public void Delete(IShapeHandle connector) => RequireVisio(connector).Shape.Delete();

    private static VisioShapeHandle RequireVisio(IShapeHandle handle) =>
        handle as VisioShapeHandle
        ?? throw new BridgeCommandException(
            BridgeErrorCodes.InternalError,
            "Connector adapter 收到非 Visio Shape handle。");

    private static (double X, double Y) ReadPin(Visio.Shape shape)
    {
        Visio.Cell? pinX = null;
        Visio.Cell? pinY = null;
        try
        {
            pinX = shape.CellsU["PinX"];
            pinY = shape.CellsU["PinY"];
            return (pinX.ResultIU, pinY.ResultIU);
        }
        finally
        {
            Release(ref pinY);
            Release(ref pinX);
        }
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
