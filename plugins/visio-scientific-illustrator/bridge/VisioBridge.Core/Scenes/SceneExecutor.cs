using System.Text.Json;
using VisioBridge.Contracts;

namespace VisioBridge.Core.Scenes;

internal static class SceneExecutor
{
    public static SceneExecutionPlan Prepare(
        DrawSceneCommand command,
        IReadOnlyCollection<string>? existingSemanticIds = null)
    {
        ArgumentNullException.ThrowIfNull(command);
        try { SceneGraphValidator.Validate(command.Scene); }
        catch (SceneContractException error)
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.InvalidArgument,
                $"{error.FieldPath}: {error.Message}");
        }
        if (command.ExistingPolicy is not ("error" or "preserve"))
            throw Invalid("existing_policy 仅允许 error 或 preserve。");

        var regionRange = SelectRegions(command.Scene, command.StartRegion, command.EndRegion);
        var selectedNodes = command.Scene.Nodes
            .Where(node => regionRange.Contains(FindRegion(command.Scene, node.Bounds)))
            .ToArray();
        var selectedIds = selectedNodes.Select(node => node.SemanticId).ToHashSet(StringComparer.Ordinal);
        var selectedConnectors = command.Scene.Connectors
            .Where(connector => selectedIds.Contains(connector.Source) && selectedIds.Contains(connector.Target))
            .ToArray();
        var existing = existingSemanticIds?.ToHashSet(StringComparer.Ordinal)
            ?? new HashSet<string>(StringComparer.Ordinal);
        var conflicts = new List<SceneExecutionConflict>();
        var steps = new List<PendingStep>
        {
            new("ensure_layers", null, null, new EnsureLayersCommand(command.Scene.Layers), -100),
        };

        foreach (var node in selectedNodes.Where(node => node is not SceneGroupNode).OrderBy(NodeRank).ThenBy(node => node.ZOrder ?? 0))
        {
            var region = FindRegion(command.Scene, node.Bounds);
            if (existing.Contains(node.SemanticId))
            {
                if (command.ExistingPolicy == "error")
                    conflicts.Add(new SceneExecutionConflict(node.SemanticId, "semantic_id 已存在，无法证明场景版本一致。", "明确选择 preserve 或先更新/删除冲突图元。"));
                continue;
            }
            steps.Add(new PendingStep(NodeCommand(node), node.SemanticId, region, NodeParameters(node), NodeRank(node)));
            if (node.Layer is not null)
                steps.Add(new PendingStep("assign_layer", node.SemanticId, region,
                    new AssignLayerCommand([node.SemanticId], node.Layer), NodeRank(node) + 1));
        }

        foreach (var group in selectedNodes.OfType<SceneGroupNode>()
            .OrderByDescending(item => ParentDepth(item, selectedNodes)))
        {
            if (existing.Contains(group.SemanticId))
            {
                if (command.ExistingPolicy == "error")
                    conflicts.Add(new SceneExecutionConflict(group.SemanticId, "group semantic_id 已存在。", "明确选择 preserve 或先删除冲突组。"));
                continue;
            }
            var members = selectedNodes.Where(node => node.ParentId == group.SemanticId)
                .Select(node => node.SemanticId).ToArray();
            if (members.Length < 2)
                throw Invalid($"Scene group {group.SemanticId} 至少需要两个直接成员。");
            var region = FindRegion(command.Scene, group.Bounds);
            steps.Add(new PendingStep(
                "group_shapes", group.SemanticId, region,
                new GroupShapesCommand(group.SemanticId, members), 250 + ParentDepth(group, selectedNodes)));
            if (group.Layer is not null)
                steps.Add(new PendingStep("assign_layer", group.SemanticId, region,
                    new AssignLayerCommand([group.SemanticId], group.Layer), 260 + ParentDepth(group, selectedNodes)));
        }

        foreach (var connector in selectedConnectors)
        {
            if (existing.Contains(connector.SemanticId))
            {
                if (command.ExistingPolicy == "error")
                    conflicts.Add(new SceneExecutionConflict(connector.SemanticId, "连接线 semantic_id 已存在。", "明确选择 preserve 或先删除冲突连接线。"));
                continue;
            }
            var sourceNode = selectedNodes.Single(node => node.SemanticId == connector.Source);
            steps.Add(new PendingStep(
                "add_connector",
                connector.SemanticId,
                FindRegion(command.Scene, sourceNode.Bounds),
                new AddConnectorCommand(
                    connector.SemanticId,
                    connector.Source,
                    connector.Target,
                    connector.Style,
                    connector.Waypoints,
                    connector.Label,
                    connector.Routing,
                    SourceAnchor: connector.SourceAnchor,
                    TargetAnchor: connector.TargetAnchor),
                300));
        }

        foreach (var node in selectedNodes.Where(node => node.ZOrder is not null))
        {
            if (existing.Contains(node.SemanticId)) continue;
            steps.Add(new PendingStep(
                "set_z_order", node.SemanticId, FindRegion(command.Scene, node.Bounds),
                new SetZOrderCommand(node.SemanticId, "absolute", node.ZOrder!.Value + 1), 500));
        }

        var ordered = steps.OrderBy(step => step.Rank).ToList();
        MarkCheckpoints(ordered, command.Scene, regionRange);
        var serialized = ordered.Select((step, index) => new SceneExecutionStep(
            index,
            step.Command,
            step.SemanticId,
            step.RegionId,
            JsonSerializer.SerializeToElement(step.Parameters, step.Parameters.GetType(), ProtocolJson.Options),
            step.CheckpointAfter)).ToArray();
        return new SceneExecutionPlan(command.Scene.SceneId, serialized, conflicts);
    }

    private static object NodeParameters(SceneNode node) => node switch
    {
        SceneShapeNode shape => new AddShapeCommand(
            shape.SemanticId, shape.Shape, shape.Bounds,
            shape.RichText is null ? shape.Label : null,
            shape.Style,
            shape.RichText),
        SceneTextNode text => new AddShapeCommand(
            text.SemanticId, "text", text.Bounds,
            text.RichText is null ? text.Text : null,
            text.Style,
            text.RichText),
        ScenePathNode path => new AddPathCommand(
            path.SemanticId, path.Bounds,
            [new PathSection(path.Commands, path.Closed, path.Closed)], path.Style),
        SceneImageNode image => new AddImageCommand(image.SemanticId, image.FilePath, image.Bounds),
        SceneComponentNode component => new AddScientificComponentCommand(
            component.SemanticId,
            component.Role ?? throw Invalid("component node 必须提供 role。"),
            component.Bounds,
            DeserializeOptions(component.Parameters)),
        SceneGroupNode => throw Invalid("group node 必须通过 group_shapes 阶段执行。"),
        _ => throw BridgeCommandException.Unsupported($"不支持 Scene node: {node.GetType().Name}"),
    };

    private static ScientificComponentOptions? DeserializeOptions(IReadOnlyDictionary<string, JsonElement>? parameters)
    {
        if (parameters is null) return null;
        return JsonSerializer.SerializeToElement(parameters, ProtocolJson.Options)
            .Deserialize<ScientificComponentOptions>(ProtocolJson.Options);
    }

    private static string NodeCommand(SceneNode node) => node switch
    {
        ScenePathNode => "add_path",
        SceneImageNode => "add_image",
        SceneComponentNode => "add_scientific_component",
        _ => "add_shape",
    };

    private static int NodeRank(SceneNode node)
    {
        if (node.Role == "background") return 0;
        if (node.Role?.Contains("panel", StringComparison.Ordinal) == true) return 100;
        if (node is SceneTextNode) return 400;
        return 200;
    }

    private static int ParentDepth(SceneNode node, IReadOnlyList<SceneNode> nodes)
    {
        var byId = nodes.ToDictionary(item => item.SemanticId, StringComparer.Ordinal);
        var depth = 0;
        var current = node;
        while (current.ParentId is not null && byId.TryGetValue(current.ParentId, out current!)) depth++;
        return depth;
    }

    private static HashSet<string> SelectRegions(ScientificScene scene, string? start, string? end)
    {
        var startIndex = start is null ? 0 : FindRegionIndex(scene, start, "start_region");
        var endIndex = end is null ? scene.Regions.Count - 1 : FindRegionIndex(scene, end, "end_region");
        if (startIndex > endIndex) throw Invalid("start_region 必须位于 end_region 之前或相同。");
        return scene.Regions.Skip(startIndex).Take(endIndex - startIndex + 1)
            .Select(region => region.Id).ToHashSet(StringComparer.Ordinal);
    }

    private static int FindRegionIndex(ScientificScene scene, string id, string field)
    {
        for (var index = 0; index < scene.Regions.Count; index++)
            if (scene.Regions[index].Id == id) return index;
        throw Invalid($"{field} 不存在：{id}");
    }

    private static string FindRegion(ScientificScene scene, PixelBox bounds)
    {
        var centerX = bounds.X + bounds.Width / 2;
        var centerY = bounds.Y + bounds.Height / 2;
        return scene.Regions.FirstOrDefault(region =>
            centerX >= region.Bounds.X && centerX <= region.Bounds.X + region.Bounds.Width
            && centerY >= region.Bounds.Y && centerY <= region.Bounds.Y + region.Bounds.Height)?.Id
            ?? scene.Regions[0].Id;
    }

    private static void MarkCheckpoints(
        IList<PendingStep> steps,
        ScientificScene scene,
        IReadOnlySet<string> selectedRegions)
    {
        foreach (var region in scene.Regions.Where(region => region.Checkpoint && selectedRegions.Contains(region.Id)))
        {
            var last = steps.Select((step, index) => (step, index))
                .Where(item => item.step.RegionId == region.Id).Select(item => item.index).LastOrDefault(-1);
            if (last >= 0) steps[last] = steps[last] with { CheckpointAfter = true };
        }
    }

    private sealed record PendingStep(
        string Command,
        string? SemanticId,
        string? RegionId,
        object Parameters,
        int Rank,
        bool CheckpointAfter = false);

    private static BridgeCommandException Invalid(string message) =>
        new(BridgeErrorCodes.InvalidArgument, message);
}
