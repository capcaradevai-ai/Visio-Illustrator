using System.Text.Json;
using System.Text.Json.Serialization;

namespace VisioBridge.Contracts;

public sealed record PreviewCommand(
    [property: JsonPropertyName("width_px")] int WidthPx = 2000);

public sealed record FitCommand(string Mode);

public sealed record SequenceOperation(
    string Type,
    JsonElement? Parameters = null);

public sealed record SequenceFailure(
    string Code,
    string Message,
    bool Retryable,
    object? Details);

public sealed record SequenceResult(
    int AppliedCount,
    int? FailedIndex,
    IReadOnlyList<object> Results,
    SequenceFailure? Error);
