using System.Text.Json.Serialization;

namespace VisioBridge.Contracts;

public sealed record ShapeSnapshot(
    string SemanticId,
    int ShapeId,
    string NameU,
    string Type,
    string Label,
    PixelBox Bounds,
    double RotationDeg,
    string? MasterNameU,
    IReadOnlyList<string> Layers,
    IReadOnlyList<string> ConnectedTo);

public sealed record PageInspectionResult(
    string DocumentName,
    string PageName,
    int PageCount,
    int ShapeCount,
    int ConnectorCount,
    bool Dirty,
    IReadOnlyList<ShapeSnapshot> Shapes,
    bool Truncated);

public sealed record UpdateShapeCommand(
    [property: JsonPropertyName("semantic_id")] string SemanticId,
    string? Label = null,
    double? X = null,
    double? Y = null,
    double? Width = null,
    double? Height = null,
    ShapeStyle? Style = null,
    [property: JsonPropertyName("rich_text")] RichText? RichText = null);

public sealed record DeleteShapeCommand(
    [property: JsonPropertyName("semantic_id")] string SemanticId,
    [property: JsonPropertyName("connector_policy")] string ConnectorPolicy);

public sealed record InspectCommand(
    [property: JsonPropertyName("max_shapes")] int MaxShapes = 500);

public sealed record ClearCommand(bool Confirm);

public sealed record CloseDocumentCommand(string Action);

public sealed record DeleteResult(
    string SemanticId,
    int DeletedConnectorCount);

public sealed record ClearResult(int DeletedShapeCount);
