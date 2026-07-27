using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using VisioTestBridge.Contracts;
using Visio = Microsoft.Office.Interop.Visio;

namespace VisioTestBridge.Core.Shapes;

internal sealed record ConnectionPointDefinition(
    double X,
    double Y,
    double DirectionX,
    double DirectionY);

internal sealed class ConnectionPointService
{
    private static readonly HashSet<char> ValidNameCharacters = new(
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_");
    private static readonly IReadOnlyDictionary<string, ConnectionPointDefinition> AnchorDefinitions =
        new Dictionary<string, ConnectionPointDefinition>(StringComparer.Ordinal)
        {
            ["top"] = new(0.5, 0, 0, 1),
            ["right"] = new(1, 0.5, 1, 0),
            ["bottom"] = new(0.5, 1, 0, -1),
            ["left"] = new(0, 0.5, -1, 0),
            ["top_right"] = new(1, 0, 1, 1),
            ["bottom_right"] = new(1, 1, 1, -1),
            ["bottom_left"] = new(0, 1, -1, -1),
            ["top_left"] = new(0, 0, -1, 1),
        };

    internal static IReadOnlyDictionary<string, ConnectionPointDefinition> StandardAnchors => AnchorDefinitions;

    internal static void EnsureStandardAnchorsForShape(Visio.Shape shape) => EnsureStandardAnchors(shape);

    internal static void AddNamedPoint(
        Visio.Shape shape,
        string name,
        double x,
        double y,
        double directionX,
        double directionY)
    {
        ValidateRowName(name);
        if (!double.IsFinite(x) || !double.IsFinite(y) || x is < 0 or > 1 || y is < 0 or > 1
            || !double.IsFinite(directionX) || !double.IsFinite(directionY)
            || directionX is < -1 or > 1 || directionY is < -1 or > 1)
            throw Invalid("连接点坐标必须在 0–1，方向必须在 -1–1 范围内。");
        AddOrUpdate(shape, name, new ConnectionPointDefinition(x, y, directionX, directionY));
    }

    public static string SelectAutoAnchor(double dx, double dy)
    {
        if (!double.IsFinite(dx) || !double.IsFinite(dy) || (dx == 0 && dy == 0))
            throw Invalid("自动连接点需要两个不同的 Shape 中心。");
        var horizontal = Math.Abs(dx);
        var vertical = Math.Abs(dy);
        var diagonal = Math.Min(horizontal, vertical) / Math.Max(horizontal, vertical) >= 0.35;
        if (diagonal)
        {
            if (dx > 0 && dy > 0) return "top_right";
            if (dx > 0 && dy < 0) return "bottom_right";
            if (dx < 0 && dy > 0) return "top_left";
            return "bottom_left";
        }
        if (horizontal >= vertical) return dx > 0 ? "right" : "left";
        return dy > 0 ? "top" : "bottom";
    }

    public static void ValidateAnchor(ConnectorAnchor anchor)
    {
        ArgumentNullException.ThrowIfNull(anchor);
        switch (anchor.Mode)
        {
            case "auto":
                if (anchor.Row is not null || anchor.X is not null || anchor.Y is not null
                    || anchor.Name is not null && !AnchorDefinitions.ContainsKey(anchor.Name))
                    throw Invalid("auto anchor 仅允许可选的标准语义 name。");
                break;
            case "connection_point":
                if ((anchor.Row is null) == (anchor.Name is null)
                    || anchor.X is not null || anchor.Y is not null)
                    throw Invalid("connection_point anchor 必须且只能提供 row 或 name。");
                if (anchor.Row is < 1 or > 32767)
                    throw Invalid("connection_point row 必须在 1–32767 范围内。");
                if (anchor.Name is not null) ValidateRowName(anchor.Name);
                break;
            case "normalized":
                if (anchor.Row is not null || anchor.Name is not null
                    || anchor.X is null || anchor.Y is null
                    || !double.IsFinite(anchor.X.Value) || !double.IsFinite(anchor.Y.Value)
                    || anchor.X is < 0 or > 1 || anchor.Y is < 0 or > 1)
                    throw Invalid("normalized anchor 必须提供 0–1 范围内的 x 和 y。");
                break;
            default:
                throw Invalid("anchor mode 仅允许 auto、connection_point 或 normalized。");
        }
    }

    public Visio.Cell Resolve(
        Visio.Shape shape,
        Visio.Shape otherShape,
        ConnectorAnchor anchor,
        string connectorSemanticId,
        string endpoint)
    {
        ValidateAnchor(anchor);
        return anchor.Mode switch
        {
            "auto" => ResolveAuto(shape, otherShape, anchor.Name),
            "connection_point" => ResolveExplicit(shape, anchor),
            "normalized" => ResolveNormalized(
                shape,
                anchor.X!.Value,
                anchor.Y!.Value,
                GeneratedRowName(connectorSemanticId, endpoint)),
            _ => throw Invalid($"不支持 anchor mode: {anchor.Mode}"),
        };
    }

    private static Visio.Cell ResolveAuto(Visio.Shape shape, Visio.Shape otherShape, string? requestedName)
    {
        EnsureStandardAnchors(shape);
        var name = requestedName;
        if (name is null)
        {
            var source = ReadPagePin(shape);
            var target = ReadPagePin(otherShape);
            name = SelectAutoAnchor(target.X - source.X, target.Y - source.Y);
        }
        return GetNamedCell(shape, name);
    }

    private static Visio.Cell ResolveExplicit(Visio.Shape shape, ConnectorAnchor anchor)
    {
        var cellName = anchor.Row is not null
            ? $"Connections.X{anchor.Row.Value}"
            : $"Connections.{anchor.Name}.X";
        if (shape.CellExistsU[cellName, (short)Visio.VisExistsFlags.visExistsAnywhere] == 0)
            throw new BridgeCommandException(
                BridgeErrorCodes.CellNotFound,
                $"Shape {shape.NameU} 不存在连接点 {cellName}。");
        return shape.CellsU[cellName];
    }

    private static Visio.Cell ResolveNormalized(Visio.Shape shape, double x, double y, string rowName)
    {
        AddOrUpdate(shape, rowName, new ConnectionPointDefinition(x, y, 0, 0));
        return GetNamedCell(shape, rowName);
    }

    private static void EnsureStandardAnchors(Visio.Shape shape)
    {
        foreach (var (name, definition) in AnchorDefinitions)
            AddOrUpdate(shape, name, definition);
    }

    private static void AddOrUpdate(
        Visio.Shape shape,
        string rowName,
        ConnectionPointDefinition definition)
    {
        var section = (short)Visio.VisSectionIndices.visSectionConnectionPts;
        if (shape.SectionExists[section, (short)Visio.VisExistsFlags.visExistsAnywhere] == 0)
            shape.AddSection(section);
        var xCellName = $"Connections.{rowName}.X";
        short row;
        if (shape.CellExistsU[xCellName, (short)Visio.VisExistsFlags.visExistsAnywhere] == 0)
        {
            row = shape.AddNamedRow(
                section,
                rowName,
                (short)Visio.VisRowTags.visTagCnnctNamed);
        }
        else
        {
            var xCell = shape.CellsU[xCellName];
            try
            {
                row = xCell.Row;
            }
            finally
            {
                Marshal.FinalReleaseComObject(xCell);
            }
        }

        SetCell(shape, section, row, (short)Visio.VisCellIndices.visCnnctX,
            $"Width*{Number(definition.X)}");
        SetCell(shape, section, row, (short)Visio.VisCellIndices.visCnnctY,
            $"Height*{Number(1 - definition.Y)}");
        SetCell(shape, section, row, (short)Visio.VisCellIndices.visCnnctDirX,
            Number(definition.DirectionX));
        SetCell(shape, section, row, (short)Visio.VisCellIndices.visCnnctDirY,
            Number(definition.DirectionY));
    }

    private static Visio.Cell GetNamedCell(Visio.Shape shape, string rowName) =>
        shape.CellsU[$"Connections.{rowName}.X"];

    private static (double X, double Y) ReadPagePin(Visio.Shape shape)
    {
        Visio.Cell? pinX = null;
        Visio.Cell? pinY = null;
        Visio.Shape? containingShape = null;
        try
        {
            pinX = shape.CellsU["PinX"];
            pinY = shape.CellsU["PinY"];
            var x = pinX.ResultIU;
            var y = pinY.ResultIU;
            containingShape = shape.ContainingShape;
            if (containingShape is not null) containingShape.XYToPage(x, y, out x, out y);
            return (x, y);
        }
        finally
        {
            Release(ref containingShape);
            Release(ref pinY);
            Release(ref pinX);
        }
    }

    private static void SetCell(
        Visio.Shape shape,
        short section,
        short row,
        short cell,
        string formula)
    {
        var target = shape.CellsSRC[section, row, cell];
        try
        {
            target.FormulaU = formula;
        }
        finally
        {
            Marshal.FinalReleaseComObject(target);
        }
    }

    private static string GeneratedRowName(string semanticId, string endpoint)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(semanticId)))
            .ToLowerInvariant()[..12];
        return $"codex_{endpoint}_{hash}";
    }

    private static void ValidateRowName(string name)
    {
        if (name.Length is < 1 or > 64
            || !char.IsLetter(name[0])
            || name.Any(character => !ValidNameCharacters.Contains(character)))
            throw Invalid("connection point name 必须以英文字母开头，且仅包含字母、数字和下划线。");
    }

    private static string Number(double value) =>
        value.ToString("0.######", CultureInfo.InvariantCulture);

    private static BridgeCommandException Invalid(string message) =>
        new(BridgeErrorCodes.InvalidArgument, message);

    private static void Release<T>(ref T? value) where T : class
    {
        if (value is not null && Marshal.IsComObject(value)) Marshal.FinalReleaseComObject(value);
        value = null;
    }
}
