using System.Text.Json.Serialization;

namespace VisioTestBridge.Contracts;

public sealed record UpdateTableCellCommand(
    [property: JsonPropertyName("semantic_id")] string SemanticId,
    int Row,
    int Column,
    TableCell Cell,
    [property: JsonPropertyName("expected_revision")] long ExpectedRevision);

public sealed record UpdateTableLayoutCommand(
    [property: JsonPropertyName("semantic_id")] string SemanticId,
    TableLayout Layout,
    [property: JsonPropertyName("expected_revision")] long ExpectedRevision);

public sealed record UpdateMatrixCellCommand(
    [property: JsonPropertyName("semantic_id")] string SemanticId,
    int Row,
    int Column,
    double Value,
    string? Fill,
    [property: JsonPropertyName("expected_revision")] long ExpectedRevision);

public sealed record ComponentUpdateResult(
    [property: JsonPropertyName("semantic_id")] string SemanticId,
    [property: JsonPropertyName("affected_semantic_ids")]
    IReadOnlyList<string> AffectedSemanticIds,
    [property: JsonPropertyName("previous_revision")] long PreviousRevision,
    [property: JsonPropertyName("new_revision")] long NewRevision);
