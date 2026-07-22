using System.Runtime.InteropServices;
using VisioBridge.Contracts;
using VisioBridge.Core.Abstractions;
using VisioBridge.Core.Interop;

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
        var routing = Validate(command);

        using var source = _repository.Resolve(command.Source);
        using var target = _repository.Resolve(command.Target);
        IShapeHandle? connector = null;
        var deleted = false;
        var phase = "create";
        try
        {
            connector = _adapter.Create(source, target, routing, command.Waypoints);
            phase = "glue";
            _adapter.Glue(
                connector,
                source,
                target,
                command.SourceConnectionPoint,
                command.TargetConnectionPoint);
            phase = "label";
            _adapter.SetLabel(connector, command.Label);
            phase = "style";
            _adapter.ApplyStyle(connector, command.Style);
            phase = "semantic_id";
            _repository.Register(connector, command.SemanticId);
            phase = "verify_connections";
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
        catch (COMException error)
        {
            var translated = ComExceptionTranslator.Translate(error);
            if (translated.Code == BridgeErrorCodes.VisioBusy) throw translated;
            if (connector is not null && !deleted)
            {
                try
                {
                    _adapter.Delete(connector);
                }
                catch (COMException)
                {
                }
            }
            throw new BridgeCommandException(
                phase == "glue" || phase == "verify_connections"
                    ? BridgeErrorCodes.ConnectorNotGlued
                    : BridgeErrorCodes.InvalidArgument,
                $"Visio connector 操作失败: {phase}。",
                details: new { phase, hresult = error.HResult, reason = error.Message });
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

    private static string Validate(AddConnectorCommand command)
    {
        if (string.Equals(command.Source, command.Target, StringComparison.Ordinal))
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.InvalidArgument,
                "connector 的 source 与 target 必须是不同 Shape。");
        }
        var routing = command.Routing ?? "dynamic";
        if (routing is not ("dynamic" or "right_angle" or "straight" or "polyline"))
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.InvalidArgument,
                "routing 仅允许 dynamic、right_angle、straight 或 polyline。");
        }
        if (command.Label is { Length: > 10000 })
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.InvalidArgument,
                "connector label 最多允许 10000 个字符。");
        }
        if (command.SourceConnectionPoint is < 1 or > 32767
            || command.TargetConnectionPoint is < 1 or > 32767)
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.InvalidArgument,
                "connection point 必须在 1–32767 范围内。");
        }
        var waypointCount = command.Waypoints?.Count ?? 0;
        if (routing == "polyline" && waypointCount is < 1 or > 64)
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.InvalidArgument,
                "polyline routing 必须提供 1–64 个 waypoints。");
        }
        if (routing != "polyline" && waypointCount > 0)
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.InvalidArgument,
                "waypoints 仅可用于 polyline routing。");
        }
        return routing;
    }
}
