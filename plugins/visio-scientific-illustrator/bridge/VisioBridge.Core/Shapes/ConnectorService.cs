using VisioBridge.Contracts;
using VisioBridge.Core.Abstractions;

namespace VisioBridge.Core.Shapes;

public sealed class ConnectorService
{
    private readonly ShapeRepository _repository;
    private readonly IConnectorAdapter _adapter;

    public ConnectorService(ShapeRepository repository, IConnectorAdapter adapter)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
    }

    public ConnectorMutationResult Add(AddConnectorCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Waypoints is { Count: > 0 })
        {
            throw BridgeCommandException.Unsupported(
                "当前版本尚未实现显式 connector waypoints。");
        }

        using var source = _repository.Resolve(command.Source);
        using var target = _repository.Resolve(command.Target);
        IShapeHandle? connector = null;
        var deleted = false;
        try
        {
            connector = _adapter.Create(source, target);
            _adapter.Glue(connector, source, target);
            _adapter.ApplyStyle(connector, command.Style);
            _repository.Register(connector, command.SemanticId);
            var connectionCount = _adapter.CountVerifiedEndpointConnections(
                connector,
                source,
                target);
            if (connectionCount != 2)
            {
                _adapter.Delete(connector);
                deleted = true;
                throw new BridgeCommandException(
                    BridgeErrorCodes.ConnectorNotGlued,
                    "Connector 两端没有同时 GlueTo 源和目标 Shape。");
            }

            return new ConnectorMutationResult(
                command.SemanticId,
                connector.Id,
                connector.NameU,
                command.Source,
                command.Target,
                connectionCount);
        }
        catch
        {
            if (connector is not null && !deleted) _adapter.Delete(connector);
            throw;
        }
        finally
        {
            connector?.Dispose();
        }
    }
}
