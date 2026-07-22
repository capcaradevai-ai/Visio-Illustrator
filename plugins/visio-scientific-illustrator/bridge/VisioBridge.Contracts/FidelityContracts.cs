using System.Text.Json.Serialization;

namespace VisioBridge.Contracts;

public sealed record ValidateFidelityCommand(
    [property: JsonPropertyName("scene_path")] string ScenePath,
    [property: JsonPropertyName("input_path")] string InputPath,
    [property: JsonPropertyName("minimum_primitive_ratio")] double MinimumPrimitiveRatio = 0.9);

public sealed record FidelityStructureMetrics(
    int ExpectedSemanticIds,
    int FoundSemanticIds,
    int ExpectedPrimitives,
    int ActualPrimitives,
    int ExpectedConnectors,
    int GluedConnectors,
    int TextMismatchCount);

public sealed record FidelityValidationResult(
    bool Valid,
    FidelityStructureMetrics Metrics,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings);
