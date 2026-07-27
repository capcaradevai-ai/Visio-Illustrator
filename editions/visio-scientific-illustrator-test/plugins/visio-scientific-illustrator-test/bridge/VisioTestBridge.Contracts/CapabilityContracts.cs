using System.Text.Json.Serialization;

namespace VisioTestBridge.Contracts;

public sealed record CapabilityVisioInfo(
    bool Installed,
    string? Version,
    string? Build,
    string Architecture);

public sealed record CapabilityEntry(
    string Id,
    string Name,
    bool Available,
    string Phase,
    string? Reason = null,
    IReadOnlyDictionary<string, string>? Metadata = null);

public sealed record CapabilityLimitation(
    string Code,
    string Message);

public sealed record CapabilityFamilySummary(
    bool Available,
    int Count,
    [property: JsonPropertyName("available_count")] int AvailableCount,
    [property: JsonPropertyName("unavailable_count")] int UnavailableCount);

public sealed record CapabilitySummary(
    string Edition,
    [property: JsonPropertyName("protocol_version")] string ProtocolVersion,
    [property: JsonPropertyName("profile_sha256")] string ProfileSha256,
    string Phase,
    CapabilityVisioInfo Visio,
    IReadOnlyDictionary<string, CapabilityFamilySummary> Families,
    IReadOnlyList<CapabilityLimitation> Limitations);

public sealed record GetCapabilityDetailsCommand(
    string Category,
    string? Cursor = null,
    int Limit = 50);

public sealed record CapabilityDetailsResult(
    string Edition,
    [property: JsonPropertyName("protocol_version")] string ProtocolVersion,
    [property: JsonPropertyName("profile_sha256")] string ProfileSha256,
    string Phase,
    string Category,
    [property: JsonPropertyName("total_count")] int TotalCount,
    int Offset,
    int Limit,
    [property: JsonPropertyName("next_cursor")] string? NextCursor,
    IReadOnlyList<CapabilityEntry> Items,
    IReadOnlyList<CapabilityLimitation> Limitations);
