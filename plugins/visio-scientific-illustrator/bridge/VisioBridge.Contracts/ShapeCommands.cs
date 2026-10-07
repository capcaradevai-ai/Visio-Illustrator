using System.Text.Json.Serialization;

namespace VisioBridge.Contracts;

public sealed record AddShapeCommand(
    [property: JsonPropertyName("semantic_id")] string SemanticId,
    string Shape,
    PixelBox Bounds,
    string? Label = null,
    ShapeStyle? Style = null,
    [property: JsonPropertyName("rich_text")] RichText? RichText = null);

public sealed record AddImageCommand(
    [property: JsonPropertyName("semantic_id")] string SemanticId,
    [property: JsonPropertyName("file_path")] string FilePath,
    PixelBox Bounds);

public sealed record AdoptShapeCommand(
    [property: JsonPropertyName("shape_id")] int ShapeId,
    [property: JsonPropertyName("semantic_id")] string SemanticId);

public sealed record AdoptShapeResult(
    string SemanticId,
    int ShapeId,
    string NameU);

public sealed record ShapeStyle(
    [property: JsonPropertyName("fill_color")] string? FillColor = null,
    [property: JsonPropertyName("fill_transparency")] double? FillTransparency = null,
    [property: JsonPropertyName("stroke_color")] string? StrokeColor = null,
    [property: JsonPropertyName("stroke_width_pt")] double? StrokeWidthPt = null,
    [property: JsonPropertyName("font_color")] string? FontColor = null,
    [property: JsonPropertyName("font_size_pt")] double? FontSizePt = null,
    [property: JsonPropertyName("rotation_deg")] double? RotationDeg = null,
    bool? Dashed = null,
    FillStyle? Fill = null,
    LineStyle? Line = null,
    [property: JsonPropertyName("text_block")] TextBlockStyle? TextBlock = null,
    ShadowStyle? Shadow = null,
    CornerStyle? Corner = null,
    [property: JsonPropertyName("opacity_percent")] double? OpacityPercent = null);

public sealed record ShapeMutationResult(
    string SemanticId,
    int ShapeId,
    string NameU,
    PixelBox Bounds,
    string Shape,
    bool Rasterized = false);
