using System.Text.Json.Serialization;

namespace VisioBridge.Contracts;

public sealed record CompileDrawioSceneCommand(
    [property: JsonPropertyName("input_path")] string InputPath,
    [property: JsonPropertyName("diagram_name")] string? DiagramName = null);

public sealed record DrawioCompileStatistics(
    int SourceVertexCount,
    int SourceEdgeCount,
    int CompiledNodeCount,
    int CompiledConnectorCount,
    int DecorativePathCount,
    int GradientCount,
    int RotatedCount,
    int HtmlLabelCount,
    int WaypointCount);

public sealed record DrawioCompileResult(
    string AbsolutePath,
    string DiagramName,
    ScientificScene Scene,
    DrawioCompileStatistics Statistics,
    IReadOnlyList<string> Warnings);
