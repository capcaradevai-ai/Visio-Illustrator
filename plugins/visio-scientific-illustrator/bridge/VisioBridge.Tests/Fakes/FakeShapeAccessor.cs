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

    public IReadOnlyList<IShapeHandle> EnumerateBoundPageShapesRecursively() =>
        _shapes.Cast<IShapeHandle>().ToArray();

    public string? ReadSemanticId(IShapeHandle shape) => shape.UserCodexId;

    public void WriteSemanticId(IShapeHandle shape, string semanticId) =>
        shape.UserCodexId = semanticId;

    public void SetUniversalName(IShapeHandle shape, string nameU) => shape.NameU = nameU;
}
