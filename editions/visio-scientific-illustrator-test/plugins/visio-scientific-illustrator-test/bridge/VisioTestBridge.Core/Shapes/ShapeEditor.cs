using VisioTestBridge.Contracts;
using VisioTestBridge.Core.Abstractions;

namespace VisioTestBridge.Core.Shapes;

public sealed class ShapeEditor
{
    private readonly IShapeEditorAdapter _adapter;

    public ShapeEditor(IShapeEditorAdapter adapter)
    {
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
    }

    public ShapeSnapshot Update(UpdateShapeCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.SemanticId);

        PixelBox? merged = null;
        if (command.X is not null
            || command.Y is not null
            || command.Width is not null
            || command.Height is not null)
        {
            var current = _adapter.Read(command.SemanticId).Bounds;
            merged = new PixelBox(
                command.X ?? current.X,
                command.Y ?? current.Y,
                command.Width ?? current.Width,
                command.Height ?? current.Height);
        }

        return _adapter.Apply(command, merged);
    }

    public DeleteResult Delete(DeleteShapeCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.ConnectorPolicy is not ("keep" or "delete"))
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.InvalidArgument,
                "connector_policy 仅允许 keep 或 delete。");
        }
        return _adapter.Delete(
            command.SemanticId,
            command.ConnectorPolicy == "delete");
    }

    public ClearResult Clear(bool confirm)
    {
        if (!confirm)
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.InvalidArgument,
                "clear 要求 confirm=true。");
        }
        return new ClearResult(_adapter.Clear());
    }
}
