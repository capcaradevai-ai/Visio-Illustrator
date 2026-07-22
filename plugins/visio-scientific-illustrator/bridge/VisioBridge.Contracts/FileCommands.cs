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
