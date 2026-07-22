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
        var plan = Validate(command);

        using var source = _repository.Resolve(command.Source);
        using var target = _repository.Resolve(command.Target);
        var avoidShapes = ResolveAvoidShapes(plan.AvoidShapeIds);
        IShapeHandle? connector = null;
        var deleted = false;
        var phase = "create";
        try
        {
            connector = _adapter.Create(source, target, plan.Routing, command.Waypoints, avoidShapes);
            phase = "glue";
            _adapter.Glue(
                connector,
                source,
                target,
                plan.SourceAnchor,
                plan.TargetAnchor,
                command.SemanticId);
            phase = "routing_options";
            _adapter.ApplyRoutingOptions(connector, command.JumpStyle);
            phase = "label";
            _adapter.SetLabel(connector, command.Label, command.LabelPosition);
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
            foreach (var avoidShape in avoidShapes) avoidShape.Dispose();
        }
    }

    private IReadOnlyList<IShapeHandle> ResolveAvoidShapes(IReadOnlyList<string> semanticIds)
    {
        var shapes = new List<IShapeHandle>(semanticIds.Count);
        try
        {
            foreach (var semanticId in semanticIds) shapes.Add(_repository.Resolve(semanticId));
            return shapes;
        }
        catch
        {
            foreach (var shape in shapes) shape.Dispose();
            throw;
        }
    }

    private static ConnectorPlan Validate(AddConnectorCommand command)
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
        if (command.SourceAnchor is not null && command.SourceConnectionPoint is not null
            || command.TargetAnchor is not null && command.TargetConnectionPoint is not null)
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.InvalidArgument,
                "source_anchor/target_anchor 不能与旧 connection point 字段混用。");
        }
        var sourceAnchor = command.SourceAnchor
            ?? (command.SourceConnectionPoint is not null
                ? new ConnectorAnchor("connection_point", Row: command.SourceConnectionPoint)
                : new ConnectorAnchor("auto"));
        var targetAnchor = command.TargetAnchor
            ?? (command.TargetConnectionPoint is not null
                ? new ConnectorAnchor("connection_point", Row: command.TargetConnectionPoint)
                : new ConnectorAnchor("auto"));
        ConnectionPointService.ValidateAnchor(sourceAnchor);
        ConnectionPointService.ValidateAnchor(targetAnchor);
        if (command.JumpStyle is not null
            && command.JumpStyle is not ("none" or "arc" or "gap" or "square"))
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.InvalidArgument,
                "jump_style 仅允许 none、arc、gap 或 square。");
        }
        if (command.LabelPosition is not null
            && (!double.IsFinite(command.LabelPosition.Value)
                || command.LabelPosition is < 0 or > 1))
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.InvalidArgument,
                "label_position 必须在 0–1 范围内。");
        }
        var avoidShapeIds = command.AvoidShapeIds ?? [];
        if (avoidShapeIds.Count > 128
            || avoidShapeIds.Any(string.IsNullOrWhiteSpace)
            || avoidShapeIds.Distinct(StringComparer.Ordinal).Count() != avoidShapeIds.Count
            || avoidShapeIds.Contains(command.Source, StringComparer.Ordinal)
            || avoidShapeIds.Contains(command.Target, StringComparer.Ordinal))
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.InvalidArgument,
                "avoid_shape_ids 必须唯一、非空、不含端点且最多 128 个。");
        }
        return new ConnectorPlan(routing, sourceAnchor, targetAnchor, avoidShapeIds);
    }

    private sealed record ConnectorPlan(
        string Routing,
        ConnectorAnchor SourceAnchor,
        ConnectorAnchor TargetAnchor,
        IReadOnlyList<string> AvoidShapeIds);
}
