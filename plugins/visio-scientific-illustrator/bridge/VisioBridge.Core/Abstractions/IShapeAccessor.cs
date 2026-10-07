namespace VisioBridge.Core.Abstractions;

public interface IShapeAccessor
{
    IReadOnlyList<IShapeHandle> EnumerateBoundPageShapesRecursively();

    IShapeHandle? FindByUniversalName(string nameU);

    IShapeHandle? FindById(int shapeId);

    string? ReadSemanticId(IShapeHandle shape);

    void WriteSemanticId(IShapeHandle shape, string semanticId);

    void SetUniversalName(IShapeHandle shape, string nameU);
}

public interface IShapeHandle : IDisposable
{
    int Id { get; }

    string? UserCodexId { get; set; }

    string NameU { get; set; }

    string Text { get; set; }
}