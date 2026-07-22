using VisioBridge.Contracts;

namespace VisioBridge.Core.Abstractions;

public sealed record ShapeEditState(PixelBox Bounds);

public interface IShapeEditorAdapter
{
    ShapeEditState Read(string semanticId);

    ShapeSnapshot Apply(UpdateShapeCommand command, PixelBox? mergedBounds);

    DeleteResult Delete(string semanticId, bool deleteConnectors);

    int Clear();
}
