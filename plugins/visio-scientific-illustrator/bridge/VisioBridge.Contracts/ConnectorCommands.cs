using System.Text.Json.Serialization;

namespace VisioBridge.Contracts;

public sealed record AddConnectorCommand(
    [property: JsonPropertyName("semantic_id")] string SemanticId,
    string Source,
    string Target,
    ConnectorStyle? Style = null,
    IReadOnlyList<PixelPoint>? Waypoints = null);

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
