using System.Text.Json.Serialization;

namespace VisioTestBridge.Contracts;

public sealed record PixelBounds(
    int X,
    int Y,
    int Width,
    int Height);

public sealed record InspectReferenceImageCommand(
    [property: JsonPropertyName("source_path")] string SourcePath);

public sealed record InspectReferenceImageResult(
    [property: JsonPropertyName("width_px")] int WidthPx,
    [property: JsonPropertyName("height_px")] int HeightPx,
    IReadOnlyList<string> Palette,
    [property: JsonPropertyName("foreground_bounds")] PixelBounds ForegroundBounds,
    [property: JsonPropertyName("pixel_orientation")] string PixelOrientation);

public sealed record CropReferenceImageCommand(
    [property: JsonPropertyName("source_path")] string SourcePath,
    PixelBounds Bounds,
    [property: JsonPropertyName("output_path")] string OutputPath);

public sealed record CropReferenceImageResult(
    [property: JsonPropertyName("output_path")] string OutputPath,
    [property: JsonPropertyName("width_px")] int WidthPx,
    [property: JsonPropertyName("height_px")] int HeightPx,
    string Sha256,
    [property: JsonPropertyName("blank_ratio")] double BlankRatio);
