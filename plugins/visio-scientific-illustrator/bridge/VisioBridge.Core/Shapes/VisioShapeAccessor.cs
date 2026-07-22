using System.Runtime.InteropServices;
using VisioBridge.Core.Abstractions;
using Visio = Microsoft.Office.Interop.Visio;

namespace VisioBridge.Core.Shapes;

internal sealed class VisioShapeAccessor : IShapeAccessor
{
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
