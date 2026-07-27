using System.Text.Json.Serialization;

namespace VisioTestBridge.Contracts;

public sealed record PathSection(
    IReadOnlyList<ScenePathCommand> Commands,
    bool Closed = false,
    bool Filled = false);

public sealed record AddPathCommand(
    [property: JsonPropertyName("semantic_id")] string SemanticId,
    PixelBox Bounds,
    IReadOnlyList<PathSection> Sections,
    ShapeStyle? Style = null);

public sealed record UpdatePathCommand(
    [property: JsonPropertyName("semantic_id")] string SemanticId,
    PixelBox? Bounds = null,
    IReadOnlyList<PathSection>? Sections = null,
    ShapeStyle? Style = null);

public sealed record PathMutationResult(
    string SemanticId,
    int ShapeId,
    string NameU,
    PixelBox Bounds,
    int SectionCount,
    int GeometryRowCount);
