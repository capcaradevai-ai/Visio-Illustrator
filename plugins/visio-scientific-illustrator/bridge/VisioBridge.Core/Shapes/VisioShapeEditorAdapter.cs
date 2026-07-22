using System.Runtime.InteropServices;
using VisioBridge.Contracts;
using VisioBridge.Core.Abstractions;
using VisioBridge.Core.Geometry;
using VisioBridge.Core.Inspection;
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
        if (command.Label is not null) shape.Text = command.Label;
        if (mergedBounds is not null)
        {
            ShapeFactory.SetGeometry(shape, _geometry.ToVisio(mergedBounds));
        }
        StyleMapper.Apply(shape, command.Style);
        return _inspector.Snapshot(shape);
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
                    if (candidate.OneD == 0) continue;
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
