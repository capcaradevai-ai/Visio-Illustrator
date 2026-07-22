using System.Text.Json.Serialization;

namespace VisioBridge.Contracts;

public sealed record AddConnectorCommand(
    [property: JsonPropertyName("semantic_id")] string SemanticId,
    string Source,
    string Target,
    ConnectorStyle? Style = null,
    IReadOnlyList<PixelPoint>? Waypoints = null,
    string? Label = null,
    string? Routing = null,
    [property: JsonPropertyName("source_connection_point")] int? SourceConnectionPoint = null,
    [property: JsonPropertyName("target_connection_point")] int? TargetConnectionPoint = null,
    [property: JsonPropertyName("source_anchor")] ConnectorAnchor? SourceAnchor = null,
    [property: JsonPropertyName("target_anchor")] ConnectorAnchor? TargetAnchor = null,
    [property: JsonPropertyName("jump_style")] string? JumpStyle = null,
    [property: JsonPropertyName("label_position")] double? LabelPosition = null,
    [property: JsonPropertyName("avoid_shape_ids")] IReadOnlyList<string>? AvoidShapeIds = null);

public sealed record ConnectorAnchor(
    string Mode,
    string? Name = null,
    int? Row = null,
    double? X = null,
    double? Y = null);

public sealed record ConnectorStyle(
    [property: JsonPropertyName("stroke_color")] string? StrokeColor = null,
    [property: JsonPropertyName("stroke_width_pt")] double? StrokeWidthPt = null,
    bool? Dashed = null,
    [property: JsonPropertyName("start_arrow")] string? StartArrow = null,
    [property: JsonPropertyName("end_arrow")] string? EndArrow = null);

public sealed record ConnectorMutationResult(
    string SemanticId,
    int ShapeId,
    string NameU,
    string Source,
    string Target,
    int ConnectionCount);
