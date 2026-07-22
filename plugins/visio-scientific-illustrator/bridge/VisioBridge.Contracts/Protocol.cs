using System.Text.Json;
using System.Text.Json.Serialization;

namespace VisioBridge.Contracts;

public sealed record BridgeRequest(long Id, string Command, JsonElement Params);

public sealed record BridgeFailure(
    string Code,
    string Message,
    bool Retryable,
    object? Details = null);

public sealed record BridgeResponse(
    long Id,
    bool Ok,
    object? Result = null,
    BridgeFailure? Error = null);

public sealed record BridgeReady(
    string Type,
    string BridgeVersion,
    int ProcessId,
    string Architecture);

public static class ProtocolJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };
}

public static class ProtocolInput
{
    public static string NormalizeLine(string line) => line.TrimStart('\uFEFF');
}
