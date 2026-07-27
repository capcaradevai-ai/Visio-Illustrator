using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using VisioTestBridge.Contracts;

namespace VisioTestBridge.Core.Capabilities;

public sealed record CapabilityProbeSnapshot(
    string Phase,
    CapabilityVisioInfo Visio,
    IReadOnlyDictionary<string, IReadOnlyList<CapabilityEntry>> Families,
    IReadOnlyList<CapabilityLimitation> Limitations);

public sealed class CapabilityService
{
    public const string Edition = "test";
    public const string ProtocolVersion = "0.4.0-test.1";

    public static readonly IReadOnlyList<string> Categories =
    [
        "masters",
        "primitives",
        "connectors",
        "components",
        "images",
        "layers",
        "containers",
        "shapesheet",
        "exports",
        "limitations",
    ];

    public CapabilitySummary GetCapabilities(CapabilityProbeSnapshot snapshot)
    {
        var normalized = Normalize(snapshot);
        var hash = ComputeProfileSha256(normalized);
        var summaries = normalized.Families.ToDictionary(
            pair => pair.Key,
            pair =>
            {
                var available = pair.Value.Count(item => item.Available);
                return new CapabilityFamilySummary(
                    available > 0,
                    pair.Value.Count,
                    available,
                    pair.Value.Count - available);
            },
            StringComparer.Ordinal);
        return new CapabilitySummary(
            Edition,
            ProtocolVersion,
            hash,
            normalized.Phase,
            normalized.Visio,
            summaries,
            normalized.Limitations);
    }

    public CapabilityDetailsResult GetCapabilityDetails(
        CapabilityProbeSnapshot snapshot,
        GetCapabilityDetailsCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!Categories.Contains(command.Category, StringComparer.Ordinal))
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.CapabilityNotAvailable,
                $"未知的 Visio 能力类别: {command.Category}",
                details: new { category = command.Category, categories = Categories });
        }
        if (command.Limit is < 1 or > 200)
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.InvalidArgument,
                "limit 必须在 1–200 范围内。");
        }

        var offset = ParseCursor(command.Cursor);
        var normalized = Normalize(snapshot);
        var entries = normalized.Families[command.Category];
        if (offset > entries.Count)
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.InvalidArgument,
                "cursor 超出当前能力类别的范围。",
                details: new { cursor = command.Cursor, total_count = entries.Count });
        }

        var page = entries.Skip(offset).Take(command.Limit).ToArray();
        var nextOffset = offset + page.Length;
        var nextCursor = nextOffset < entries.Count
            ? nextOffset.ToString(CultureInfo.InvariantCulture)
            : null;
        return new CapabilityDetailsResult(
            Edition,
            ProtocolVersion,
            ComputeProfileSha256(normalized),
            normalized.Phase,
            command.Category,
            entries.Count,
            offset,
            command.Limit,
            nextCursor,
            page,
            normalized.Limitations);
    }

    public CapabilityProbeSnapshot ProbeInstalled(string pluginRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginRoot);
        var installed = Type.GetTypeFromProgID("Visio.InvisibleApp", throwOnError: false) is not null;
        var architecture = RuntimeInformation.ProcessArchitecture == Architecture.X64
            ? "x64"
            : "unknown";
        var stencilPath = Path.Combine(
            Path.GetFullPath(pluginRoot),
            "assets",
            "visio-scientific-masters.vssx");
        var stencilAvailable = File.Exists(stencilPath);
        var limitations = new List<CapabilityLimitation>();
        if (!installed)
        {
            limitations.Add(new(
                BridgeErrorCodes.VisioNotInstalled,
                "Microsoft Visio COM ProgID 未注册。"));
        }
        if (!stencilAvailable)
        {
            limitations.Add(new(
                BridgeErrorCodes.MasterNotFound,
                "Test Edition 内置 Visio stencil 不存在。"));
        }
        limitations.Add(new(
            "SESSION_DETAILS_PENDING",
            "启动 Test Edition 自有 Visio 会话后才能报告会话与文档能力。"));

        var families = CreateInstalledFamilies(installed, stencilAvailable);
        return new CapabilityProbeSnapshot(
            "installed",
            new CapabilityVisioInfo(installed, null, null, architecture),
            families,
            limitations);
    }

    public CapabilityProbeSnapshot ProbeSession(
        CapabilityProbeSnapshot installed,
        string version,
        string build,
        IReadOnlyList<CapabilityEntry> openMasters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentException.ThrowIfNullOrWhiteSpace(build);
        ArgumentNullException.ThrowIfNull(openMasters);

        var families = CloneFamilies(installed.Families);
        families["masters"] = families["masters"]
            .Concat(openMasters)
            .GroupBy(item => item.Id, StringComparer.Ordinal)
            .Select(group => group.Last())
            .OrderBy(item => item.Id, StringComparer.Ordinal)
            .ToArray();
        var limitations = installed.Limitations
            .Where(item => item.Code != "SESSION_DETAILS_PENDING")
            .ToArray();
        return new CapabilityProbeSnapshot(
            "session",
            installed.Visio with { Version = version, Build = build },
            families,
            limitations);
    }

    public CapabilityProbeSnapshot ProbeDocument(
        CapabilityProbeSnapshot session,
        string pageName,
        int widthPx,
        int heightPx,
        IReadOnlyList<string> layers,
        int containerCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pageName);
        ArgumentNullException.ThrowIfNull(layers);
        if (widthPx <= 0 || heightPx <= 0 || containerCount < 0)
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.InvalidArgument,
                "文档能力快照包含无效的页面尺寸或容器数量。");
        }

        var families = CloneFamilies(session.Families);
        families["layers"] = families["layers"]
            .Concat(layers
                .OrderBy(name => name, StringComparer.Ordinal)
                .Select(name => new CapabilityEntry(
                    $"document-layer:{name}",
                    name,
                    true,
                    "document",
                    Metadata: new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["page_name"] = pageName,
                    })))
            .ToArray();
        families["containers"] = families["containers"]
            .Concat([
                new CapabilityEntry(
                    "document-container-count",
                    "Current document containers",
                    true,
                    "document",
                    Metadata: new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["page_name"] = pageName,
                        ["count"] = containerCount.ToString(CultureInfo.InvariantCulture),
                    }),
            ])
            .ToArray();
        families["shapesheet"] = families["shapesheet"]
            .Concat([
                new CapabilityEntry(
                    "current-pagesheet",
                    "Current PageSheet",
                    true,
                    "document",
                    Metadata: new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["page_name"] = pageName,
                        ["width_px"] = widthPx.ToString(CultureInfo.InvariantCulture),
                        ["height_px"] = heightPx.ToString(CultureInfo.InvariantCulture),
                    }),
            ])
            .ToArray();
        return new CapabilityProbeSnapshot(
            "document",
            session.Visio,
            families,
            session.Limitations);
    }

    private static Dictionary<string, IReadOnlyList<CapabilityEntry>> CreateInstalledFamilies(
        bool visioInstalled,
        bool stencilAvailable)
    {
        var families = Categories.ToDictionary(
            category => category,
            _ => (IReadOnlyList<CapabilityEntry>)Array.Empty<CapabilityEntry>(),
            StringComparer.Ordinal);
        var visioReason = visioInstalled ? null : "Microsoft Visio 未安装或 COM 未注册。";
        CapabilityEntry VisioCapability(string id, string name) => new(
            id,
            name,
            visioInstalled,
            visioInstalled ? "installed" : "unavailable",
            visioReason);

        var mastersAvailable = visioInstalled && stencilAvailable;
        var masterReason = mastersAvailable
            ? null
            : !visioInstalled
                ? visioReason
                : "Test Edition 内置 Visio stencil 不存在。";
        families["masters"] = new[]
        {
            "rounded_rectangle", "diamond", "cylinder", "hexagon", "triangle",
            "parallelogram", "cloud", "tensor_face", "video_frame", "latent_vector",
            "snowflake", "router", "antenna", "stage_panel", "container",
        }.Select(id => new CapabilityEntry(
            id,
            id.Replace('_', ' '),
            mastersAvailable,
            mastersAvailable ? "installed" : "unavailable",
            masterReason)).ToArray();
        families["primitives"] = new[]
        {
            "rectangle", "ellipse", "line", "text", "arc", "polyline",
            "bezier", "freeform", "group",
        }.Select(id => VisioCapability(id, id)).ToArray();
        families["connectors"] = new[]
        {
            "straight", "right_angle", "curved", "polyline", "named-anchor",
            "normalized-anchor", "double-ended-glue", "line-jumps",
        }.Select(id => VisioCapability(id, id)).ToArray();
        families["components"] = new[]
        {
            "tensor_cube", "video_frame", "latent_vector", "snowflake",
            "wireless_link",
        }.Select(id => VisioCapability(id, id)).ToArray();
        families["images"] =
        [
            VisioCapability("local-raster-import", "Local raster import"),
            new CapabilityEntry(
                "native-picture-crop",
                "Native Visio picture crop",
                false,
                "unavailable",
                "Test Edition 当前 live 合同未实现 Visio 图片裁切。"),
        ];
        families["layers"] =
        [
            VisioCapability("assign-layer", "Assign layer"),
            VisioCapability("replace-layer-membership", "Replace layer membership"),
        ];
        families["containers"] =
        [
            VisioCapability("native-container", "Native Visio container"),
            VisioCapability("group", "Editable Visio group"),
        ];
        families["shapesheet"] =
        [
            VisioCapability("read-cells", "Read controlled ShapeSheet cells"),
            VisioCapability("write-cells", "Write controlled ShapeSheet cells"),
            VisioCapability("connection-points", "Editable connection points"),
            VisioCapability("geometry-sections", "Editable geometry sections"),
        ];
        families["exports"] =
        [
            VisioCapability("png", "PNG export"),
            VisioCapability("svg", "SVG export"),
            VisioCapability("pdf", "PDF export"),
            VisioCapability("vsdx", "Editable VSDX save"),
        ];
        return families;
    }

    private static CapabilityProbeSnapshot Normalize(CapabilityProbeSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.Phase is not ("installed" or "session" or "document"))
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.InvalidArgument,
                $"无效的 capability phase: {snapshot.Phase}");
        }

        var families = Categories.ToDictionary(
            category => category,
            category => snapshot.Families.TryGetValue(category, out var entries)
                ? (IReadOnlyList<CapabilityEntry>)entries
                    .OrderBy(item => item.Id, StringComparer.Ordinal)
                    .ToArray()
                : Array.Empty<CapabilityEntry>(),
            StringComparer.Ordinal);
        var limitationEntries = snapshot.Limitations
            .OrderBy(item => item.Code, StringComparer.Ordinal)
            .Select(item => new CapabilityEntry(
                item.Code,
                item.Code,
                false,
                "unavailable",
                item.Message))
            .ToArray();
        families["limitations"] = families["limitations"]
            .Concat(limitationEntries)
            .GroupBy(item => item.Id, StringComparer.Ordinal)
            .Select(group => group.Last())
            .OrderBy(item => item.Id, StringComparer.Ordinal)
            .ToArray();
        return snapshot with
        {
            Families = families,
            Limitations = snapshot.Limitations
                .OrderBy(item => item.Code, StringComparer.Ordinal)
                .ToArray(),
        };
    }

    private static Dictionary<string, IReadOnlyList<CapabilityEntry>> CloneFamilies(
        IReadOnlyDictionary<string, IReadOnlyList<CapabilityEntry>> source) =>
        Categories.ToDictionary(
            category => category,
            category => source.TryGetValue(category, out var entries)
                ? (IReadOnlyList<CapabilityEntry>)entries.ToArray()
                : Array.Empty<CapabilityEntry>(),
            StringComparer.Ordinal);

    private static int ParseCursor(string? cursor)
    {
        if (cursor is null) return 0;
        if (!int.TryParse(
                cursor,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var offset)
            || offset < 0
            || !string.Equals(
                offset.ToString(CultureInfo.InvariantCulture),
                cursor,
                StringComparison.Ordinal))
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.InvalidArgument,
                "cursor 必须是规范的非负十进制偏移量。");
        }
        return offset;
    }

    private static string ComputeProfileSha256(CapabilityProbeSnapshot snapshot)
    {
        var payload = new
        {
            edition = Edition,
            protocol_version = ProtocolVersion,
            phase = snapshot.Phase,
            visio = snapshot.Visio,
            families = snapshot.Families,
            limitations = snapshot.Limitations,
        };
        var json = JsonSerializer.SerializeToUtf8Bytes(payload, ProtocolJson.Options);
        using var document = JsonDocument.Parse(json);
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            WriteCanonical(writer, document.RootElement);
        }
        return Convert.ToHexString(SHA256.HashData(buffer.ToArray())).ToLowerInvariant();
    }

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject()
                             .OrderBy(item => item.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonical(writer, property.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    WriteCanonical(writer, item);
                }
                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                writer.WriteStringValue(element.GetString());
                break;
            case JsonValueKind.Number:
                writer.WriteRawValue(element.GetRawText());
                break;
            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;
            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;
            case JsonValueKind.Null:
                writer.WriteNullValue();
                break;
            default:
                throw new InvalidOperationException(
                    $"capability profile 包含不支持的 JSON 类型: {element.ValueKind}");
        }
    }
}
