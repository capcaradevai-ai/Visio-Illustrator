using System.Text.Json;
using System.Text.Json.Serialization;

namespace VisioBridge.Contracts;

public sealed record SceneCanvas(
    [property: JsonPropertyName("width_px")] int WidthPx,
    [property: JsonPropertyName("height_px")] int HeightPx,
    double Dpi);

public sealed record ScientificScene(
    [property: JsonPropertyName("scene_id")] string SceneId,
    SceneCanvas Canvas,
    [property: JsonPropertyName("style_tokens")] IReadOnlyDictionary<string, string> StyleTokens,
    IReadOnlyList<string> Layers,
    IReadOnlyList<SceneNode> Nodes,
    IReadOnlyList<SceneConnector> Connectors,
    IReadOnlyList<SceneRegion> Regions);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(SceneShapeNode), "shape")]
[JsonDerivedType(typeof(SceneTextNode), "text")]
[JsonDerivedType(typeof(ScenePathNode), "path")]
[JsonDerivedType(typeof(SceneImageNode), "image")]
[JsonDerivedType(typeof(SceneComponentNode), "component")]
[JsonDerivedType(typeof(SceneGroupNode), "group")]
public abstract record SceneNode(
    [property: JsonPropertyName("semantic_id")] string SemanticId,
    PixelBox Bounds,
    string? Layer = null,
    [property: JsonPropertyName("z_order")] int? ZOrder = null,
    string? Role = null,
    [property: JsonPropertyName("parent_id")] string? ParentId = null);

public sealed record SceneShapeNode(
    string SemanticId,
    PixelBox Bounds,
    string Shape,
    string? Label = null,
    ShapeStyle? Style = null,
    [property: JsonPropertyName("rich_text")] RichText? RichText = null,
    string? Layer = null,
    int? ZOrder = null,
    string? Role = null,
    string? ParentId = null)
    : SceneNode(SemanticId, Bounds, Layer, ZOrder, Role, ParentId);

public sealed record SceneTextNode(
    string SemanticId,
    PixelBox Bounds,
    string Text,
    ShapeStyle? Style = null,
    [property: JsonPropertyName("rich_text")] RichText? RichText = null,
    string? Layer = null,
    int? ZOrder = null,
    string? Role = null,
    string? ParentId = null)
    : SceneNode(SemanticId, Bounds, Layer, ZOrder, Role, ParentId);

public sealed record ScenePathNode(
    string SemanticId,
    PixelBox Bounds,
    IReadOnlyList<ScenePathCommand> Commands,
    bool Closed = false,
    ShapeStyle? Style = null,
    string? Layer = null,
    int? ZOrder = null,
    string? Role = null,
    string? ParentId = null)
    : SceneNode(SemanticId, Bounds, Layer, ZOrder, Role, ParentId);

public sealed record SceneImageNode(
    string SemanticId,
    PixelBox Bounds,
    [property: JsonPropertyName("file_path")] string FilePath,
    string? Layer = null,
    int? ZOrder = null,
    string? Role = null,
    string? ParentId = null)
    : SceneNode(SemanticId, Bounds, Layer, ZOrder, Role, ParentId);

public sealed record SceneComponentNode(
    string SemanticId,
    PixelBox Bounds,
    string Role,
    IReadOnlyDictionary<string, JsonElement>? Parameters = null,
    string? Layer = null,
    int? ZOrder = null,
    string? ParentId = null)
    : SceneNode(SemanticId, Bounds, Layer, ZOrder, Role, ParentId);

public sealed record SceneGroupNode(
    string SemanticId,
    PixelBox Bounds,
    string? Layer = null,
    int? ZOrder = null,
    string? Role = null,
    string? ParentId = null)
    : SceneNode(SemanticId, Bounds, Layer, ZOrder, Role, ParentId);

public sealed record ScenePathCommand(
    string Op,
    double? X = null,
    double? Y = null,
    double? X1 = null,
    double? Y1 = null,
    double? X2 = null,
    double? Y2 = null,
    double? Rx = null,
    double? Ry = null,
    [property: JsonPropertyName("rotation_deg")] double? RotationDeg = null,
    [property: JsonPropertyName("large_arc")] bool? LargeArc = null,
    bool? Sweep = null);

public sealed record SceneConnector(
    [property: JsonPropertyName("semantic_id")] string SemanticId,
    string Source,
    string Target,
    string Routing,
    bool? Glued = null,
    string? Label = null,
    ConnectorStyle? Style = null,
    IReadOnlyList<PixelPoint>? Waypoints = null,
    [property: JsonPropertyName("source_anchor")] ConnectorAnchor? SourceAnchor = null,
    [property: JsonPropertyName("target_anchor")] ConnectorAnchor? TargetAnchor = null);

public sealed record SceneRegion(
    string Id,
    PixelBox Bounds,
    bool Checkpoint);

public sealed record DrawSceneCommand(
    ScientificScene Scene,
    [property: JsonPropertyName("start_region")] string? StartRegion = null,
    [property: JsonPropertyName("end_region")] string? EndRegion = null,
    [property: JsonPropertyName("stop_after_checkpoint")] bool StopAfterCheckpoint = false,
    [property: JsonPropertyName("existing_policy")] string ExistingPolicy = "error");

public sealed record EnsureLayersCommand(IReadOnlyList<string> Layers);

public sealed record EnsureLayersResult(IReadOnlyList<string> Layers, int CreatedCount);

public sealed record SceneExecutionStep(
    int Index,
    string Command,
    [property: JsonPropertyName("semantic_id")] string? SemanticId,
    [property: JsonPropertyName("region_id")] string? RegionId,
    JsonElement Parameters,
    [property: JsonPropertyName("checkpoint_after")] bool CheckpointAfter = false);

public sealed record SceneExecutionConflict(
    string SemanticId,
    string Reason,
    string Resolution);

public sealed record SceneExecutionPlan(
    string SceneId,
    IReadOnlyList<SceneExecutionStep> Steps,
    IReadOnlyList<SceneExecutionConflict> Conflicts);

public sealed class SceneContractException : Exception
{
    public SceneContractException(string code, string fieldPath, string message)
        : base(message)
    {
        Code = code;
        FieldPath = fieldPath;
    }

    public string Code { get; }
    public string FieldPath { get; }
}

public static class SceneGraphValidator
{
    private static readonly HashSet<string> PathOperations = new(StringComparer.Ordinal)
    {
        "M", "L", "C", "A", "Z",
    };

    public static void Validate(ScientificScene scene)
    {
        if (string.IsNullOrWhiteSpace(scene.SceneId)) Fail("$.scene_id", "scene_id 不能为空。");
        if (scene.Canvas.WidthPx <= 0) Fail("$.canvas.width_px", "画布宽度必须大于零。");
        if (scene.Canvas.HeightPx <= 0) Fail("$.canvas.height_px", "画布高度必须大于零。");
        if (!double.IsFinite(scene.Canvas.Dpi) || scene.Canvas.Dpi <= 0) Fail("$.canvas.dpi", "DPI 必须是正有限数值。");

        var layers = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < scene.Layers.Count; index++)
        {
            var layer = scene.Layers[index];
            if (string.IsNullOrWhiteSpace(layer) || !layers.Add(layer))
                Fail($"$.layers[{index}]", "图层名称必须非空且唯一。");
        }

        var identifiers = new HashSet<string>(StringComparer.Ordinal);
        var nodes = new Dictionary<string, SceneNode>(StringComparer.Ordinal);
        for (var index = 0; index < scene.Nodes.Count; index++)
        {
            var node = scene.Nodes[index];
            var path = $"$.nodes[{index}]";
            AddIdentifier(identifiers, node.SemanticId, $"{path}.semantic_id");
            nodes.Add(node.SemanticId, node);
            ValidateBounds(node.Bounds, scene.Canvas, $"{path}.bounds");
            if (node.Layer is not null && !layers.Contains(node.Layer))
                Fail($"{path}.layer", $"未声明图层: {node.Layer}");
            if (node is ScenePathNode pathNode) ValidatePath(pathNode, path);
        }

        ValidateParentGraph(scene.Nodes, nodes);

        for (var index = 0; index < scene.Connectors.Count; index++)
        {
            var connector = scene.Connectors[index];
            var path = $"$.connectors[{index}]";
            AddIdentifier(identifiers, connector.SemanticId, $"{path}.semantic_id");
            if (!nodes.ContainsKey(connector.Source)) Fail($"{path}.source", "连接线源 Shape 不存在。");
            if (!nodes.ContainsKey(connector.Target)) Fail($"{path}.target", "连接线目标 Shape 不存在。");
            if (connector.Source == connector.Target) Fail(path, "连接线源和目标必须不同。");
        }

        var regionIds = new HashSet<string>(StringComparer.Ordinal);
        var hasCheckpoint = false;
        for (var index = 0; index < scene.Regions.Count; index++)
        {
            var region = scene.Regions[index];
            var path = $"$.regions[{index}]";
            if (string.IsNullOrWhiteSpace(region.Id) || !regionIds.Add(region.Id))
                Fail($"{path}.id", "区域 ID 必须非空且唯一。");
            ValidateBounds(region.Bounds, scene.Canvas, $"{path}.bounds");
            hasCheckpoint |= region.Checkpoint;
        }
        if (!hasCheckpoint) Fail("$.regions", "至少需要一个预览检查点区域。");
    }

    private static void ValidatePath(ScenePathNode node, string path)
    {
        if (node.Commands.Count == 0) Fail($"{path}.commands", "路径至少需要一个命令。");
        if (!string.Equals(node.Commands[0].Op, "M", StringComparison.Ordinal))
            Fail($"{path}.commands[0].op", "路径首命令必须是 M。");

        for (var index = 0; index < node.Commands.Count; index++)
        {
            var command = node.Commands[index];
            var commandPath = $"{path}.commands[{index}]";
            if (!PathOperations.Contains(command.Op)) Fail($"{commandPath}.op", $"不支持路径命令: {command.Op}");
            ValidatePoint(command.X, command.Y, node.Bounds, commandPath);
            ValidatePoint(command.X1, command.Y1, node.Bounds, commandPath);
            ValidatePoint(command.X2, command.Y2, node.Bounds, commandPath);
        }
    }

    private static void ValidatePoint(double? x, double? y, PixelBox bounds, string path)
    {
        if (x is null && y is null) return;
        if (x is null || y is null || !double.IsFinite(x.Value) || !double.IsFinite(y.Value)
            || x.Value < 0 || y.Value < 0 || x.Value > bounds.Width || y.Value > bounds.Height)
            Fail(path, "路径局部坐标必须位于节点 bounds 内。");
    }

    private static void ValidateParentGraph(IReadOnlyList<SceneNode> sceneNodes, IReadOnlyDictionary<string, SceneNode> nodes)
    {
        for (var index = 0; index < sceneNodes.Count; index++)
        {
            var node = sceneNodes[index];
            if (node.ParentId is not null && !nodes.ContainsKey(node.ParentId))
                Fail($"$.nodes[{index}].parent_id", "父节点不存在。");

            var seen = new HashSet<string>(StringComparer.Ordinal) { node.SemanticId };
            var current = node;
            while (current.ParentId is not null)
            {
                if (!seen.Add(current.ParentId))
                    Fail($"$.nodes[{index}].parent_id", "节点父子关系存在循环。");
                current = nodes[current.ParentId];
            }
        }
    }

    private static void ValidateBounds(PixelBox bounds, SceneCanvas canvas, string path)
    {
        if (!double.IsFinite(bounds.X) || !double.IsFinite(bounds.Y)
            || !double.IsFinite(bounds.Width) || !double.IsFinite(bounds.Height)
            || bounds.X < 0 || bounds.Y < 0 || bounds.Width <= 0 || bounds.Height <= 0
            || bounds.X + bounds.Width > canvas.WidthPx
            || bounds.Y + bounds.Height > canvas.HeightPx)
            Fail(path, "边界必须为画布内的正有限矩形。");
    }

    private static void AddIdentifier(HashSet<string> identifiers, string identifier, string path)
    {
        if (string.IsNullOrWhiteSpace(identifier)) Fail(path, "语义 ID 不能为空。");
        if (!identifiers.Add(identifier))
            throw new SceneContractException("DUPLICATE_SEMANTIC_ID", path, $"重复语义 ID: {identifier}");
    }

    private static void Fail(string path, string message) =>
        throw new SceneContractException("INVALID_SCENE", path, message);
}
