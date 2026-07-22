using VisioBridge.Contracts;
using VisioBridge.Core.Abstractions;
using VisioBridge.Core.Shapes;

namespace VisioBridge.Tests.Fakes;

internal sealed class FakePageFixture
{
    private FakePageFixture(FakeEditableShape shape, ShapeEditor editor)
    {
        Shape = shape;
        Editor = editor;
    }

    public FakeEditableShape Shape { get; }

    public ShapeEditor Editor { get; }

    public static FakePageFixture WithShape(
        string semanticId,
        string label,
        double x,
        string fill)
    {
        var shape = new FakeEditableShape(
            semanticId,
            label,
            new PixelBox(x, 100, 200, 80),
            new ShapeStyle(FillColor: fill));
        return new FakePageFixture(shape, new ShapeEditor(new FakeShapeEditorAdapter(shape)));
    }
}

internal sealed class FakeEditableShape
{
    public FakeEditableShape(
        string semanticId,
        string label,
        PixelBox bounds,
        ShapeStyle style)
    {
        SemanticId = semanticId;
        Label = label;
        Bounds = bounds;
        Style = style;
    }

    public string SemanticId { get; }
    public string Label { get; set; }
    public PixelBox Bounds { get; set; }
    public ShapeStyle Style { get; set; }
    public bool Deleted { get; set; }
}

internal sealed class FakeShapeEditorAdapter : IShapeEditorAdapter
{
    private readonly FakeEditableShape _shape;

    public FakeShapeEditorAdapter(FakeEditableShape shape) => _shape = shape;

    public ShapeEditState Read(string semanticId)
    {
        EnsureMatch(semanticId);
        return new ShapeEditState(_shape.Bounds);
    }

    public ShapeSnapshot Apply(UpdateShapeCommand command, PixelBox? mergedBounds)
    {
        EnsureMatch(command.SemanticId);
        if (command.Label is not null) _shape.Label = command.Label;
        if (mergedBounds is not null) _shape.Bounds = mergedBounds;
        if (command.Style is not null)
        {
            _shape.Style = _shape.Style with
            {
                FillColor = command.Style.FillColor ?? _shape.Style.FillColor,
                FillTransparency = command.Style.FillTransparency ?? _shape.Style.FillTransparency,
                StrokeColor = command.Style.StrokeColor ?? _shape.Style.StrokeColor,
                StrokeWidthPt = command.Style.StrokeWidthPt ?? _shape.Style.StrokeWidthPt,
                FontColor = command.Style.FontColor ?? _shape.Style.FontColor,
                FontSizePt = command.Style.FontSizePt ?? _shape.Style.FontSizePt,
                RotationDeg = command.Style.RotationDeg ?? _shape.Style.RotationDeg,
                Dashed = command.Style.Dashed ?? _shape.Style.Dashed,
            };
        }
        return Snapshot();
    }

    public DeleteResult Delete(string semanticId, bool deleteConnectors)
    {
        EnsureMatch(semanticId);
        _shape.Deleted = true;
        return new DeleteResult(semanticId, deleteConnectors ? 1 : 0);
    }

    public int Clear()
    {
        _shape.Deleted = true;
        return 1;
    }

    private ShapeSnapshot Snapshot() => new(
        _shape.SemanticId,
        1,
        $"Codex_{_shape.SemanticId}",
        "rectangle",
        _shape.Label,
        _shape.Bounds,
        _shape.Style.RotationDeg ?? 0,
        null,
        [],
        []);

    private void EnsureMatch(string semanticId)
    {
        if (!string.Equals(semanticId, _shape.SemanticId, StringComparison.Ordinal))
        {
            throw new VisioBridge.Core.BridgeCommandException(
                VisioBridge.Core.BridgeErrorCodes.ShapeNotFound,
                $"找不到 Shape: {semanticId}");
        }
    }
}
