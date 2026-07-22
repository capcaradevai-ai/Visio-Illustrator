using System.Text.Json.Serialization;

namespace VisioBridge.Contracts;

public sealed record SaveCommand(
    [property: JsonPropertyName("output_path")] string OutputPath,
    bool Overwrite = false);

public sealed record SaveResult(
    string AbsolutePath,
    long Bytes,
    bool Overwritten,
    bool Validated);

public sealed record ValidateFileCommand(
    [property: JsonPropertyName("input_path")] string InputPath);

public sealed record FileValidationResult(
    string AbsolutePath,
    int PageCount,
    int ShapeCount,
    int ConnectorCount,
    bool Valid);

public sealed record InspectFileCommand(
    [property: JsonPropertyName("input_path")] string InputPath,
    [property: JsonPropertyName("max_shapes")] int MaxShapes = 500);

public sealed record ExportFileCommand(
    [property: JsonPropertyName("input_path")] string InputPath,
    string Format,
    [property: JsonPropertyName("output_path")] string OutputPath,
    [property: JsonPropertyName("page_scope")] string PageScope,
    bool Overwrite = false,
    int Width = 2000);

public sealed record FileInspectionResult(
    string AbsolutePath,
    string VisioVersion,
    bool ReadOnly,
    IReadOnlyList<PageInspectionResult> Pages,
    IReadOnlyList<string> ValidationWarnings);

public sealed record FileExportResult(
    string InputPath,
    string OutputPath,
    string Format,
    string PageScope,
    long Bytes,
    bool Overwritten);
