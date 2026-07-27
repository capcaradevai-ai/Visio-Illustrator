using System.Text.Json.Serialization;

namespace VisioTestBridge.Contracts;

[JsonConverter(typeof(JsonStringEnumConverter<RegionQualityState>))]
public enum RegionQualityState
{
    Planned,
    Drawing,
    AwaitingAudit,
    Failed,
    Correcting,
    Passed,
}

[JsonConverter(typeof(JsonStringEnumConverter<RegionQualityEvent>))]
public enum RegionQualityEvent
{
    BeginDrawing,
    FinishDrawing,
    AuditFailed,
    BeginCorrection,
    FinishCorrection,
    AuditPassed,
    PageMutated,
}

public sealed record RegionQualitySnapshot(
    [property: JsonPropertyName("region_id")] string RegionId,
    RegionQualityState State,
    [property: JsonPropertyName("page_revision")] long PageRevision,
    [property: JsonPropertyName("last_passed_revision")] long? LastPassedRevision);

public sealed record QualityGateCommand(
    [property: JsonPropertyName("region_id")] string RegionId);

public sealed record QualityGateResult(
    [property: JsonPropertyName("region_id")] string RegionId,
    [property: JsonPropertyName("page_revision")] long PageRevision,
    bool Passed);

public sealed record StructureHashResult(
    [property: JsonPropertyName("page_revision")] long PageRevision,
    [property: JsonPropertyName("structure_snapshot_sha256")] string StructureSnapshotSha256,
    [property: JsonPropertyName("shape_count")] int ShapeCount);
