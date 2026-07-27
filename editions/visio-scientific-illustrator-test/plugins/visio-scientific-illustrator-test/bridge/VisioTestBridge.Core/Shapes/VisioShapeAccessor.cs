using System.Runtime.InteropServices;
using VisioTestBridge.Core.Abstractions;
using Visio = Microsoft.Office.Interop.Visio;

namespace VisioTestBridge.Core.Shapes;

internal sealed class VisioShapeAccessor : IShapeAccessor
{
    private const int ShapeNameNotFound = unchecked((int)0x86DB0904);
    private readonly Func<Visio.Page> _pageProvider;

    public VisioShapeAccessor(Func<Visio.Page> pageProvider)
    {
        _pageProvider = pageProvider ?? throw new ArgumentNullException(nameof(pageProvider));
    }

    public IReadOnlyList<IShapeHandle> EnumerateBoundPageShapesRecursively()
    {
        var result = new List<IShapeHandle>();
        try
        {
            Enumerate(_pageProvider().Shapes, result);
            return result;
        }
        catch
        {
            foreach (var shape in result) shape.Dispose();
            throw;
        }
    }

    public IShapeHandle? FindByUniversalName(string nameU)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nameU);
        var page = _pageProvider();
        var shapes = page.Shapes;
        try
        {
            var direct = TryItemU(shapes, nameU);
            if (direct is not null) return new VisioShapeHandle(direct);
        }
        finally
        {
            Marshal.ReleaseComObject(shapes);
        }

        Visio.Selection? groups = null;
        try
        {
            groups = page.CreateSelection(
                Visio.VisSelectionTypes.visSelTypeByType,
                Visio.VisSelectMode.visSelModeSkipSuper,
                (short)Visio.VisTypeSelectionTypes.visTypeSelGroup);
            for (var index = 1; index <= groups.Count; index++)
            {
                var group = groups[index];
                try
                {
                    var children = group.Shapes;
                    try
                    {
                        var nested = FindInCollection(children, nameU);
                        if (nested is not null) return new VisioShapeHandle(nested);
                    }
                    finally
                    {
                        Marshal.ReleaseComObject(children);
                    }
                }
                finally
                {
                    Marshal.ReleaseComObject(group);
                }
            }
            return null;
        }
        finally
        {
            if (groups is not null) Marshal.ReleaseComObject(groups);
        }
    }

    public string? ReadSemanticId(IShapeHandle shape) => shape.UserCodexId;

    public void WriteSemanticId(IShapeHandle shape, string semanticId) =>
        shape.UserCodexId = semanticId;

    public void SetUniversalName(IShapeHandle shape, string nameU) => shape.NameU = nameU;

    private static void Enumerate(Visio.Shapes shapes, ICollection<IShapeHandle> output)
    {
        try
        {
            for (var index = 1; index <= shapes.Count; index++)
            {
                var shape = shapes[index];
                var handle = new VisioShapeHandle(shape);
                output.Add(handle);

                var children = shape.Shapes;
                if (children.Count > 0) Enumerate(children, output);
                else Marshal.ReleaseComObject(children);
            }
        }
        finally
        {
            Marshal.ReleaseComObject(shapes);
        }
    }

    private static Visio.Shape? FindInCollection(Visio.Shapes shapes, string nameU)
    {
        var direct = TryItemU(shapes, nameU);
        if (direct is not null) return direct;

        for (var index = 1; index <= shapes.Count; index++)
        {
            var shape = shapes[index];
            try
            {
                if (shape.Type != (short)Visio.VisShapeTypes.visTypeGroup) continue;
                var children = shape.Shapes;
                try
                {
                    var nested = FindInCollection(children, nameU);
                    if (nested is not null) return nested;
                }
                finally
                {
                    Marshal.ReleaseComObject(children);
                }
            }
            finally
            {
                Marshal.ReleaseComObject(shape);
            }
        }
        return null;
    }

    private static Visio.Shape? TryItemU(Visio.Shapes shapes, string nameU)
    {
        try
        {
            return shapes.ItemU[nameU];
        }
        catch (COMException error) when (error.HResult == ShapeNameNotFound)
        {
            return null;
        }
        catch (COMException error)
        {
            throw Interop.ComExceptionTranslator.Translate(error);
        }
    }
}

internal sealed class VisioShapeHandle : IShapeHandle
{
    private Visio.Shape? _shape;

    public VisioShapeHandle(Visio.Shape shape)
    {
        _shape = shape ?? throw new ArgumentNullException(nameof(shape));
    }

    public int Id => Shape.ID;

    public string? UserCodexId
    {
        get
        {
            if (Shape.CellExistsU["User.CodexId", (short)Visio.VisExistsFlags.visExistsAnywhere] == 0)
            {
                return null;
            }
            var cell = Shape.CellsU["User.CodexId"];
            try
            {
                return cell.ResultStrU[(object)Visio.VisUnitCodes.visUnitsString];
            }
            finally
            {
                Marshal.ReleaseComObject(cell);
            }
        }
        set
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            if (Shape.SectionExists[
                    (short)Visio.VisSectionIndices.visSectionUser,
                    (short)Visio.VisExistsFlags.visExistsAnywhere] == 0)
            {
                Shape.AddSection((short)Visio.VisSectionIndices.visSectionUser);
            }
            if (Shape.CellExistsU["User.CodexId", (short)Visio.VisExistsFlags.visExistsAnywhere] == 0)
            {
                Shape.AddNamedRow(
                    (short)Visio.VisSectionIndices.visSectionUser,
                    "CodexId",
                    (short)Visio.VisRowTags.visTagDefault);
            }
            var cell = Shape.CellsU["User.CodexId"];
            try
            {
                cell.FormulaU = $"\"{value.Replace("\"", "\"\"")}\"";
            }
            finally
            {
                Marshal.ReleaseComObject(cell);
            }
        }
    }

    public string NameU
    {
        get => Shape.NameU;
        set => Shape.NameU = value;
    }

    public string Text
    {
        get => Shape.Text;
        set => Shape.Text = value;
    }

    internal Visio.Shape Shape => _shape
        ?? throw new ObjectDisposedException(nameof(VisioShapeHandle));

    public void Dispose()
    {
        if (_shape is null) return;
        Marshal.ReleaseComObject(_shape);
        _shape = null;
    }
}
