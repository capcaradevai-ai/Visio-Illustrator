using System.Text.Json;
using System.Text.Json.Serialization;

namespace VisioTestBridge.Contracts;

public sealed record CompositeComponentContract(
    [property: JsonPropertyName("semantic_id")] string SemanticId,
    [property: JsonPropertyName("component_type")] string ComponentType,
    [property: JsonPropertyName("region_id")] string RegionId,
    PixelBox Bounds,
    JsonElement? Style,
    [property: JsonPropertyName("expected_revision")] long ExpectedRevision,
    JsonElement? Data = null,
    [property: JsonPropertyName("schema_version")] string SchemaVersion = "1.0",
    [property: JsonPropertyName("layout_version")] int LayoutVersion = 1);

public sealed record CompositeComponentMutationResult(
    [property: JsonPropertyName("semantic_id")] string SemanticId,
    [property: JsonPropertyName("component_type")] string ComponentType,
    [property: JsonPropertyName("child_semantic_ids")]
    IReadOnlyList<string> ChildSemanticIds,
    [property: JsonPropertyName("previous_revision")] long PreviousRevision,
    [property: JsonPropertyName("new_revision")] long NewRevision);
