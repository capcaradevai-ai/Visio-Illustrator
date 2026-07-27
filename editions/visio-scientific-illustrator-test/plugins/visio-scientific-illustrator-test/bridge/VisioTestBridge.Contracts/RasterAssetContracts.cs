using System.Text.Json.Serialization;

namespace VisioTestBridge.Contracts;

public sealed record RasterAssetDeclaration(
    [property: JsonPropertyName("asset_id")] string AssetId,
    [property: JsonPropertyName("source_type")] string SourceType,
    [property: JsonPropertyName("raster_reason")] string RasterReason,
    [property: JsonPropertyName("source_is_tightly_cropped")] bool SourceIsTightlyCropped,
    [property: JsonPropertyName("atomic_raster_unit")] bool AtomicRasterUnit,
    [property: JsonPropertyName("contains_reconstructable_content")] bool ContainsReconstructableContent,
    [property: JsonPropertyName("decomposition_note")] string DecompositionNote,
    [property: JsonPropertyName("editable_scope")] string EditableScope,
    [property: JsonPropertyName("source_reference_sha256")] string? SourceReferenceSha256,
    [property: JsonPropertyName("source_bounds")] PixelBox? SourceBounds,
    [property: JsonPropertyName("asset_sha256")] string AssetSha256,
    [property: JsonPropertyName("semantic_role")] string SemanticRole,
    [property: JsonPropertyName("provenance_note")] string? ProvenanceNote = null);

public sealed record RasterValidationResult(
    string AssetId,
    string AssetSha256,
    string SourceType,
    string SemanticRole);
