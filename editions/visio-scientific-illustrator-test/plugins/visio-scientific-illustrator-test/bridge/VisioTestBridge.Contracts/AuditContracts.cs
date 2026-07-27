using System.Text.Json.Serialization;

namespace VisioTestBridge.Contracts;

[JsonConverter(typeof(JsonStringEnumConverter<AuditSeverity>))]
public enum AuditSeverity
{
    Hard,
    Warning,
    Info,
}

[JsonConverter(typeof(JsonStringEnumConverter<AuditFindingStatus>))]
public enum AuditFindingStatus
{
    Open,
    Resolved,
    Waived,
    Stale,
}

public sealed record AuditEvidence(
    [property: JsonPropertyName("page_revision")] long PageRevision,
    [property: JsonPropertyName("region_id")] string RegionId,
    [property: JsonPropertyName("preview_sha256")] string PreviewSha256,
    [property: JsonPropertyName("reference_crop_sha256")] string ReferenceCropSha256,
    [property: JsonPropertyName("structure_snapshot_sha256")] string StructureSnapshotSha256,
    [property: JsonPropertyName("audit_profile")] string AuditProfile);

public sealed record FigureAuditObject(
    [property: JsonPropertyName("semantic_id")] string SemanticId,
    [property: JsonPropertyName("expected_text")] string? ExpectedText,
    [property: JsonPropertyName("actual_text")] string? ActualText,
    [property: JsonPropertyName("is_inside_canvas")] bool IsInsideCanvas,
    [property: JsonPropertyName("text_overflows")] bool TextOverflows,
    [property: JsonPropertyName("metadata_valid")] bool MetadataValid,
    [property: JsonPropertyName("is_connector")] bool IsConnector,
    [property: JsonPropertyName("source_glued")] bool? SourceGlued,
    [property: JsonPropertyName("target_glued")] bool? TargetGlued,
    [property: JsonPropertyName("unexpected_overlap_with")] IReadOnlyList<string> UnexpectedOverlapWith,
    [property: JsonPropertyName("alignment_error")] double AlignmentError,
    [property: JsonPropertyName("connector_clearance")] double? ConnectorClearance,
    [property: JsonPropertyName("raster_declaration_valid")] bool? RasterDeclarationValid);

public sealed record FigureAuditInput(
    [property: JsonPropertyName("page_revision")] long PageRevision,
    [property: JsonPropertyName("region_id")] string RegionId,
    AuditEvidence Evidence,
    IReadOnlyList<FigureAuditObject> Objects,
    [property: JsonPropertyName("regional_visual_score")] double RegionalVisualScore,
    [property: JsonPropertyName("minimum_regional_visual_score")] double MinimumRegionalVisualScore,
    [property: JsonPropertyName("maximum_alignment_error")] double MaximumAlignmentError = 0.25,
    [property: JsonPropertyName("minimum_connector_clearance")] double MinimumConnectorClearance = 0.1);

public sealed record AuditSafePatch(
    string Operation,
    [property: JsonPropertyName("object_ids")] IReadOnlyList<string> ObjectIds,
    [property: JsonPropertyName("expected_revision")] long ExpectedRevision,
    IReadOnlyDictionary<string, string> Arguments);

public sealed record AuditFinding(
    string Id,
    AuditSeverity Severity,
    string Category,
    [property: JsonPropertyName("region_id")] string RegionId,
    [property: JsonPropertyName("object_ids")] IReadOnlyList<string> ObjectIds,
    [property: JsonPropertyName("observed_revision")] long ObservedRevision,
    IReadOnlyDictionary<string, string> Evidence,
    [property: JsonPropertyName("required_change")] string RequiredChange,
    [property: JsonPropertyName("acceptance_conditions")] IReadOnlyList<string> AcceptanceConditions,
    [property: JsonPropertyName("safe_patch")] AuditSafePatch? SafePatch,
    AuditFindingStatus Status);

public sealed record FigureAuditResult(
    [property: JsonPropertyName("overall_score")] double OverallScore,
    [property: JsonPropertyName("region_score")] double RegionScore,
    [property: JsonPropertyName("hard_count")] int HardCount,
    [property: JsonPropertyName("warning_count")] int WarningCount,
    [property: JsonPropertyName("info_count")] int InfoCount,
    IReadOnlyList<AuditFinding> Findings,
    [property: JsonPropertyName("model_review_required")] bool ModelReviewRequired,
    [property: JsonPropertyName("model_review_reasons")] IReadOnlyList<string> ModelReviewReasons,
    [property: JsonPropertyName("can_pass")] bool CanPass,
    AuditEvidence Evidence);

public sealed record GetAuditFindingsCommand(
    [property: JsonPropertyName("region_id")] string? RegionId = null,
    AuditFindingStatus? Status = null);

public sealed record GetAuditFindingCommand(string Id);

public sealed record SetAuditDecisionCommand(
    string Id,
    string Decision,
    string? Reason,
    [property: JsonPropertyName("expected_revision")] long ExpectedRevision);

public sealed record AuditFindingListResult(
    [property: JsonPropertyName("page_revision")] long PageRevision,
    IReadOnlyList<AuditFinding> Findings);

public sealed record ApplySafePatchCommand(
    [property: JsonPropertyName("finding_id")] string FindingId,
    [property: JsonPropertyName("expected_revision")] long ExpectedRevision);

public sealed record SafePatchResult(
    [property: JsonPropertyName("finding_id")] string FindingId,
    string Operation,
    [property: JsonPropertyName("object_ids")] IReadOnlyList<string> ObjectIds,
    [property: JsonPropertyName("previous_revision")] long PreviousRevision,
    [property: JsonPropertyName("new_revision")] long NewRevision,
    [property: JsonPropertyName("requires_reaudit")] bool RequiresReaudit);
