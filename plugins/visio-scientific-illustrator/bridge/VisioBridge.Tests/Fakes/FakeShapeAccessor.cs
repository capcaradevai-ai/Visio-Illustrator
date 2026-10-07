using VisioBridge.Core.Abstractions;

namespace VisioBridge.Tests.Fakes;

internal sealed class FakeShape : IShapeHandle
{
    public FakeShape(int id, string? userCodexId)
    {
        Id = id;
        UserCodexId = userCodexId;
    }

    public int Id { get; }

    public string? UserCodexId { get; set; }

    public string NameU { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    public void Dispose()
    {
    }
}

internal sealed class FakeShapeAccessor : IShapeAccessor
{
    private readonly IReadOnlyList<FakeShape> _shapes;

    private FakeShapeAccessor(IReadOnlyList<FakeShape> shapes)
    {
        _shapes = shapes;
    }

    public static FakeShapeAccessor WithShapes(params FakeShape[] shapes) => new(shapes);

    public int EnumerationCount { get; private set; }

    public int UniversalNameLookupCount { get; private set; }

    public int IdLookupCount { get; private set; }

    public IReadOnlyList<IShapeHandle> EnumerateBoundPageShapesRecursively()
    {
        EnumerationCount++;
        return _shapes.Cast<IShapeHandle>().ToArray();
    }

    public IShapeHandle? FindByUniversalName(string nameU)
    {
        UniversalNameLookupCount++;
        return _shapes.FirstOrDefault(shape => shape.NameU == nameU);
    }

    public IShapeHandle? FindById(int shapeId)
    {
        IdLookupCount++;
        return _shapes.FirstOrDefault(shape => shape.Id == shapeId);
    }

    public string? ReadSemanticId(IShapeHandle shape) => shape.UserCodexId;

    public void WriteSemanticId(IShapeHandle shape, string semanticId) =>
        shape.UserCodexId = semanticId;

    public void SetUniversalName(IShapeHandle shape, string nameU)
    {
        shape.NameU = _shapes.Any(item => item.Id != shape.Id && item.NameU == nameU)
            ? $"{nameU}.2"
            : nameU;
    }
}