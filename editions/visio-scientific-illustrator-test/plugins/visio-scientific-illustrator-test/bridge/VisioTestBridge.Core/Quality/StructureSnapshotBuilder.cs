using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace VisioTestBridge.Core.Quality;

public sealed record SnapshotBounds(double X, double Y, double Width, double Height);

public sealed record SnapshotStyle(string? FillColor, string? LineColor, double? LineWidth);

public sealed record SnapshotConnectorEndpoints(string SourceId, string TargetId);

public sealed record SnapshotGlueState(bool SourceGlued, bool TargetGlued);

public sealed record StructureSnapshotItem(
    string SemanticId,
    string Kind,
    SnapshotBounds Bounds,
    string? Text,
    SnapshotStyle? StyleSummary,
    string? GroupParent,
    string? Layer,
    int? ZOrder,
    SnapshotConnectorEndpoints? ConnectorEndpoints,
    SnapshotGlueState? GlueState,
    string? ImageDeclarationHash,
    string? ComponentType,
    string? RegionId,
    long? LastModifiedRevision);

public sealed record StructureSnapshot(string CanonicalJson, string Sha256);

public static class StructureSnapshotBuilder
{
    public static StructureSnapshot Build(IEnumerable<StructureSnapshotItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var ordered = items
            .OrderBy(item => item.SemanticId, StringComparer.Ordinal)
            .ToArray();

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartArray();
            foreach (var item in ordered)
            {
                Validate(item);
                WriteItem(writer, item);
            }
            writer.WriteEndArray();
        }

        var bytes = stream.ToArray();
        return new StructureSnapshot(
            Encoding.UTF8.GetString(bytes),
            Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
    }

    private static void WriteItem(Utf8JsonWriter writer, StructureSnapshotItem item)
    {
        writer.WriteStartObject();
        writer.WriteString("semantic_id", item.SemanticId);
        writer.WriteString("kind", item.Kind);
        writer.WritePropertyName("bounds");
        writer.WriteStartObject();
        writer.WriteNumber("x", Round(item.Bounds.X));
        writer.WriteNumber("y", Round(item.Bounds.Y));
        writer.WriteNumber("width", Round(item.Bounds.Width));
        writer.WriteNumber("height", Round(item.Bounds.Height));
        writer.WriteEndObject();
        WriteOptional(writer, "text", item.Text);
        if (item.StyleSummary is not null)
        {
            writer.WritePropertyName("style_summary");
            writer.WriteStartObject();
            WriteOptional(writer, "fill_color", NormalizeColor(item.StyleSummary.FillColor));
            WriteOptional(writer, "line_color", NormalizeColor(item.StyleSummary.LineColor));
            if (item.StyleSummary.LineWidth is not null)
                writer.WriteNumber("line_width", Round(item.StyleSummary.LineWidth.Value));
            writer.WriteEndObject();
        }
        WriteOptional(writer, "group_parent", item.GroupParent);
        WriteOptional(writer, "layer", item.Layer);
        if (item.ZOrder is not null)
            writer.WriteNumber("z_order", item.ZOrder.Value);
        if (item.ConnectorEndpoints is not null)
        {
            writer.WritePropertyName("connector_endpoints");
            writer.WriteStartObject();
            writer.WriteString("source_id", item.ConnectorEndpoints.SourceId);
            writer.WriteString("target_id", item.ConnectorEndpoints.TargetId);
            writer.WriteEndObject();
        }
        if (item.GlueState is not null)
        {
            writer.WritePropertyName("glue_state");
            writer.WriteStartObject();
            writer.WriteBoolean("source_glued", item.GlueState.SourceGlued);
            writer.WriteBoolean("target_glued", item.GlueState.TargetGlued);
            writer.WriteEndObject();
        }
        WriteOptional(writer, "image_declaration_hash", item.ImageDeclarationHash);
        WriteOptional(writer, "component_type", item.ComponentType);
        WriteOptional(writer, "region_id", item.RegionId);
        if (item.LastModifiedRevision is not null)
            writer.WriteNumber("last_modified_revision", item.LastModifiedRevision.Value);
        writer.WriteEndObject();
    }

    private static void Validate(StructureSnapshotItem item)
    {
        if (string.IsNullOrWhiteSpace(item.SemanticId) || string.IsNullOrWhiteSpace(item.Kind))
            throw new BridgeCommandException(
                BridgeErrorCodes.InvalidArgument,
                "结构快照对象必须包含 semantic_id 和 kind。");
        if (!double.IsFinite(item.Bounds.X)
            || !double.IsFinite(item.Bounds.Y)
            || !double.IsFinite(item.Bounds.Width)
            || !double.IsFinite(item.Bounds.Height)
            || item.Bounds.Width < 0
            || item.Bounds.Height < 0)
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.InvalidArgument,
                $"结构快照对象 {item.SemanticId} 的边界无效。");
        }
    }

    private static double Round(double value) =>
        Math.Round(value, 4, MidpointRounding.AwayFromZero);

    private static string? NormalizeColor(string? value)
    {
        if (value is null)
            return null;
        if (value.Length != 7
            || value[0] != '#'
            || value.Skip(1).Any(character => !Uri.IsHexDigit(character)))
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.InvalidArgument,
                $"结构快照颜色必须是 #RRGGBB：{value}");
        }
        return value.ToUpperInvariant();
    }

    private static void WriteOptional(Utf8JsonWriter writer, string name, string? value)
    {
        if (value is not null)
            writer.WriteString(name, value);
    }
}
