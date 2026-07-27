using System.Text.Json.Serialization;

namespace VisioTestBridge.Contracts;

public sealed record SetZOrderCommand(
    [property: JsonPropertyName("semantic_id")] string SemanticId,
    string Action,
    int? Position = null);

public sealed record AssignLayerCommand(
    [property: JsonPropertyName("semantic_ids")] IReadOnlyList<string> SemanticIds,
    string Layer,
    [property: JsonPropertyName("create_if_missing")] bool CreateIfMissing = true,
    [property: JsonPropertyName("replace_existing")] bool ReplaceExisting = false);

public sealed record AlignShapesCommand(
    [property: JsonPropertyName("semantic_ids")] IReadOnlyList<string> SemanticIds,
    string Alignment,
    [property: JsonPropertyName("reference_semantic_id")] string? ReferenceSemanticId = null);

public sealed record DistributeShapesCommand(
    [property: JsonPropertyName("semantic_ids")] IReadOnlyList<string> SemanticIds,
    string Axis,
    string Distribution,
    [property: JsonPropertyName("preserve_outer_bounds")] bool PreserveOuterBounds = true);

public sealed record MatchSizeCommand(
    [property: JsonPropertyName("semantic_ids")] IReadOnlyList<string> SemanticIds,
    [property: JsonPropertyName("reference_semantic_id")] string ReferenceSemanticId,
    string Dimension);

public sealed record SetProtectionCommand(
    [property: JsonPropertyName("semantic_ids")] IReadOnlyList<string> SemanticIds,
    ProtectionFields Fields);

public sealed record ProtectionFields(
    [property: JsonPropertyName("move_x")] bool? MoveX = null,
    [property: JsonPropertyName("move_y")] bool? MoveY = null,
    bool? Width = null,
    bool? Height = null,
    bool? Rotate = null,
    [property: JsonPropertyName("text_edit")] bool? TextEdit = null,
    bool? Delete = null,
    bool? Format = null,
    bool? Group = null,
    bool? Select = null,
    bool? Aspect = null,
    [property: JsonPropertyName("vertex_edit")] bool? VertexEdit = null,
    bool? Begin = null,
    bool? End = null);

public sealed record DuplicateShapeCommand(
    [property: JsonPropertyName("semantic_id")] string SemanticId,
    [property: JsonPropertyName("new_semantic_id")] string NewSemanticId,
    [property: JsonPropertyName("offset_x")] double OffsetX = 20,
    [property: JsonPropertyName("offset_y")] double OffsetY = 20);

public sealed record LayoutShapeChange(
    string SemanticId,
    PixelBox BeforeBounds,
    PixelBox AfterBounds,
    int BeforeZOrder,
    int AfterZOrder);

public sealed record LayoutMutationResult(
    string Operation,
    IReadOnlyList<LayoutShapeChange> Changes,
    IReadOnlyDictionary<string, string>? ChildSemanticIdMap = null);
