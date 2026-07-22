using System.Runtime.InteropServices;
using VisioBridge.Contracts;
using VisioBridge.Core.Geometry;
using VisioBridge.Core.Interop;
using Visio = Microsoft.Office.Interop.Visio;

namespace VisioBridge.Core.Inspection;

internal sealed class PageInspector
{
    private readonly Visio.Document _document;
    private readonly Visio.Page _page;
    private readonly GeometryConverter _geometry;

    public PageInspector(
        Visio.Document document,
        Visio.Page page,
        GeometryConverter geometry)
    {
        _document = document;
        _page = page;
        _geometry = geometry;
    }

    public PageInspectionResult Inspect(int maxShapes)
    {
        if (maxShapes is < 1 or > 5000)
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.InvalidArgument,
                "max_shapes 必须在 1–5000 范围内。");
        }

        var snapshots = new List<ShapeSnapshot>();
        var total = 0;
        var connectors = 0;
        Enumerate(_page.Shapes, maxShapes, snapshots, ref total, ref connectors);

        var pages = _document.Pages;
        try
        {
            return new PageInspectionResult(
                _document.Name,
                _page.Name,
                pages.Count,
                total - connectors,
                connectors,
                !_document.Saved,
                snapshots,
                total > snapshots.Count);
        }
        finally
        {
            Marshal.ReleaseComObject(pages);
        }
    }

    public ShapeSnapshot Resolve(string semanticId)
    {
        using var handle = new Shapes.ShapeRepository(
            new Shapes.VisioShapeAccessor(() => _page)).Resolve(semanticId);
        return Snapshot(((Shapes.VisioShapeHandle)handle).Shape);
    }

    internal ShapeSnapshot Snapshot(Visio.Shape shape)
    {
        var semanticId = ReadSemanticId(shape) ?? string.Empty;
        var bounds = ReadBounds(shape);
        var layers = ReadLayers(shape);
        var connectedTo = ReadConnectedSemanticIds(shape);
        Visio.Master? master = null;
        string? masterName = null;
        try
        {
            master = shape.Master;
            masterName = master?.NameU;
        }
        catch (COMException error)
        {
            var translated = ComExceptionTranslator.Translate(error);
            if (translated.Code == BridgeErrorCodes.VisioBusy)
            {
                throw translated;
            }
            masterName = null;
        }
        finally
        {
            if (master is not null) Marshal.ReleaseComObject(master);
        }

        return new ShapeSnapshot(
            semanticId,
            shape.ID,
            shape.NameU,
            shape.OneD != 0
                ? "connector"
                : shape.Type == (short)Visio.VisShapeTypes.visTypeGroup ? "group" : "shape",
            shape.Text ?? string.Empty,
            bounds,
            ReadCell(shape, "Angle", Visio.VisUnitCodes.visDegrees),
            masterName,
            layers,
            connectedTo,
            shape.Index,
            StyleInspector.Read(shape),
            GeometryInspector.ReadSummary(shape),
            ReadGroup(shape),
            StyleInspector.ReadProtection(shape),
            GeometryInspector.ReadConnectionPointNames(shape),
            ReadSourceMetadata(shape));
    }

    internal PixelBox ReadBounds(Visio.Shape shape)
    {
        shape.BoundingBox(
            (short)(Visio.VisBoundingBoxArgs.visBBoxDrawingCoords
                | Visio.VisBoundingBoxArgs.visBBoxUprightWH),
            out var left,
            out var bottom,
            out var right,
            out var top);
        var width = Math.Max(right - left, 0.000001);
        var height = Math.Max(top - bottom, 0.000001);
        return _geometry.ToPixelsAllowingOverflow(new VisioBox(
            (left + right) / 2,
            (bottom + top) / 2,
            width,
            height));
    }

    private void Enumerate(
        Visio.Shapes shapes,
        int maxShapes,
        ICollection<ShapeSnapshot> snapshots,
        ref int total,
        ref int connectors)
    {
        try
        {
            for (var index = 1; index <= shapes.Count; index++)
            {
                var shape = shapes[index];
                try
                {
                    total++;
                    if (shape.OneD != 0) connectors++;
                    if (snapshots.Count < maxShapes) snapshots.Add(Snapshot(shape));

                    var children = shape.Shapes;
                    if (children.Count > 0)
                    {
                        Enumerate(children, maxShapes, snapshots, ref total, ref connectors);
                    }
                    else
                    {
                        Marshal.ReleaseComObject(children);
                    }
                }
                finally
                {
                    Marshal.ReleaseComObject(shape);
                }
            }
        }
        finally
        {
            Marshal.ReleaseComObject(shapes);
        }
    }

    private static string? ReadSemanticId(Visio.Shape shape)
    {
        if (shape.CellExistsU[
                "User.CodexId",
                (short)Visio.VisExistsFlags.visExistsAnywhere] == 0)
        {
            return null;
        }
        var cell = shape.CellsU["User.CodexId"];
        try
        {
            return cell.ResultStrU[(object)Visio.VisUnitCodes.visUnitsString];
        }
        finally
        {
            Marshal.ReleaseComObject(cell);
        }
    }

    private static double ReadCell(
        Visio.Shape shape,
        string cellName,
        Visio.VisUnitCodes units)
    {
        if (shape.CellExistsU[
                cellName,
                (short)Visio.VisExistsFlags.visExistsAnywhere] == 0)
        {
            return 0;
        }
        var cell = shape.CellsU[cellName];
        try
        {
            return cell.Result[(object)units];
        }
        finally
        {
            Marshal.ReleaseComObject(cell);
        }
    }

    private static IReadOnlyList<string> ReadLayers(Visio.Shape shape)
    {
        var result = new List<string>();
        for (short index = 1; index <= shape.LayerCount; index++)
        {
            var layer = shape.Layer[index];
            try
            {
                result.Add(layer.Name);
            }
            finally
            {
                Marshal.ReleaseComObject(layer);
            }
        }
        return result;
    }

    private static IReadOnlyList<string> ReadConnectedSemanticIds(Visio.Shape shape)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        var connects = shape.Connects;
        try
        {
            for (var index = 1; index <= connects.Count; index++)
            {
                var connect = connects[index];
                Visio.Shape? target = null;
                try
                {
                    target = connect.ToSheet;
                    var semanticId = ReadSemanticId(target);
                    if (!string.IsNullOrEmpty(semanticId)) result.Add(semanticId);
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
        return result.ToArray();
    }

    private static GroupSummary ReadGroup(Visio.Shape shape)
    {
        Visio.Shape? parent = null;
        Visio.Shapes? children = null;
        try
        {
            parent = shape.ContainingShape;
            children = shape.Shapes;
            var childIds = new List<string>();
            for (var index = 1; index <= children.Count; index++)
            {
                var child = children[index];
                try
                {
                    var id = ReadSemanticId(child);
                    if (!string.IsNullOrWhiteSpace(id)) childIds.Add(id);
                }
                finally { Marshal.FinalReleaseComObject(child); }
            }
            return new GroupSummary(parent is null ? null : ReadSemanticId(parent), childIds);
        }
        finally
        {
            if (children is not null) Marshal.FinalReleaseComObject(children);
            if (parent is not null) Marshal.FinalReleaseComObject(parent);
        }
    }

    private static IReadOnlyDictionary<string, string> ReadSourceMetadata(Visio.Shape shape)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        const string cellName = "User.DrawioCellId";
        if (shape.CellExistsU[cellName, (short)Visio.VisExistsFlags.visExistsAnywhere] == 0) return result;
        var cell = shape.CellsU[cellName];
        try
        {
            result["drawio_cell_id"] = cell.ResultStrU[(object)Visio.VisUnitCodes.visUnitsString];
            return result;
        }
        finally { Marshal.FinalReleaseComObject(cell); }
    }
}
