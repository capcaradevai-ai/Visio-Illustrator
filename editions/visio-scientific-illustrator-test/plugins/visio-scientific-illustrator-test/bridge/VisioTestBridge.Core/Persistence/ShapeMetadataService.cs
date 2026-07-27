using System.Globalization;
using System.Text.Json;
using VisioTestBridge.Contracts;

namespace VisioTestBridge.Core.Persistence;

public interface IRasterMetadataStore
{
    void WriteUserCell(string cellName, string value);

    void WriteShapeData(string cellName, string label, string value);
}

public sealed record ShapeQualityMetadata(
    string CodexId,
    string RegionId,
    string ObjectRole,
    long LastModifiedRevision);

public sealed class ShapeMetadataService(IQualityCellStore cells)
{
    public const string Edition = "test";
    public const string ProtocolVersion = "0.4.0-test.1";

    public void Write(ShapeQualityMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        if (string.IsNullOrWhiteSpace(metadata.CodexId)
            || string.IsNullOrWhiteSpace(metadata.RegionId)
            || string.IsNullOrWhiteSpace(metadata.ObjectRole)
            || metadata.LastModifiedRevision < 0)
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.InvalidArgument,
                "对象质量元数据必须包含标识、区域、角色和非负修订号。");
        }

        cells.Write("User.CodexId", metadata.CodexId);
        cells.Write("User.VsiEdition", Edition);
        cells.Write("User.VsiProtocolVersion", ProtocolVersion);
        cells.Write("User.VsiRegionId", metadata.RegionId);
        cells.Write("User.VsiObjectRole", metadata.ObjectRole);
        cells.Write(
            "User.VsiLastModifiedRevision",
            metadata.LastModifiedRevision.ToString(CultureInfo.InvariantCulture));
    }

    public static void WriteRaster(
        IRasterMetadataStore store,
        RasterAssetDeclaration declaration)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(declaration);
        var sourceBounds = declaration.SourceBounds is null
            ? string.Empty
            : JsonSerializer.Serialize(new
            {
                x = declaration.SourceBounds.X,
                y = declaration.SourceBounds.Y,
                width = declaration.SourceBounds.Width,
                height = declaration.SourceBounds.Height,
            }, ProtocolJson.Options);
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["VsiRasterAssetId"] = declaration.AssetId,
            ["VsiRasterSourceType"] = declaration.SourceType,
            ["VsiRasterReason"] = declaration.RasterReason,
            ["VsiRasterTightlyCropped"] = FormatBoolean(declaration.SourceIsTightlyCropped),
            ["VsiRasterAtomicUnit"] = FormatBoolean(declaration.AtomicRasterUnit),
            ["VsiRasterContainsReconstructable"] =
                FormatBoolean(declaration.ContainsReconstructableContent),
            ["VsiRasterDecompositionNote"] = declaration.DecompositionNote,
            ["VsiRasterEditableScope"] = declaration.EditableScope,
            ["VsiRasterSourceReferenceSha256"] =
                declaration.SourceReferenceSha256 ?? string.Empty,
            ["VsiRasterSourceBoundsJson"] = sourceBounds,
            ["VsiRasterAssetSha256"] = declaration.AssetSha256,
            ["VsiRasterSemanticRole"] = declaration.SemanticRole,
        };

        foreach (var (suffix, value) in values)
            store.WriteUserCell($"User.{suffix}", value);
        foreach (var (suffix, value) in values)
            store.WriteShapeData($"Prop.{suffix}", suffix, value);
    }

    private static string FormatBoolean(bool value) => value ? "true" : "false";
}
