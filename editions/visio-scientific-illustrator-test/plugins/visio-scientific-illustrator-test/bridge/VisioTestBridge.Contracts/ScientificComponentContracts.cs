using System.Text.Json.Serialization;

namespace VisioTestBridge.Contracts;

public sealed record AddScientificComponentCommand(
    [property: JsonPropertyName("semantic_id")] string SemanticId,
    string Component,
    PixelBox Bounds,
    ScientificComponentOptions? Options = null);

public sealed record ScientificComponentOptions(
    string? Title = null,
    string? Label = null,
    [property: JsonPropertyName("primary_color")] string? PrimaryColor = null,
    [property: JsonPropertyName("secondary_color")] string? SecondaryColor = null,
    int? Rows = null,
    int? Columns = null,
    int? Count = null,
    [property: JsonPropertyName("show_labels")] bool? ShowLabels = null,
    [property: JsonPropertyName("image_path")] string? ImagePath = null,
    [property: JsonPropertyName("image_declaration")] RasterAssetDeclaration? ImageDeclaration = null,
    string? Direction = null);

public sealed record ScientificComponentMutationResult(
    string SemanticId,
    string Component,
    int ShapeId,
    IReadOnlyList<string> ChildSemanticIds,
    int PrimitiveCount,
    PixelBox Bounds,
    IReadOnlyList<string> ConnectionPoints);
