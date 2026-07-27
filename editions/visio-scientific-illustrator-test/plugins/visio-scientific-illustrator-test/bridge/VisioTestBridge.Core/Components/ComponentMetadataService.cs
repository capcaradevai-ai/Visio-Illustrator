using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using VisioTestBridge.Contracts;

namespace VisioTestBridge.Core.Components;

internal sealed class ComponentMetadataService
{
    internal const string ComponentTypeCell = "User.VsiComponentType";
    internal const string SchemaVersionCell = "User.VsiComponentSchemaVersion";
    internal const string DataSha256Cell = "User.VsiComponentDataSha256";
    internal const string LayoutVersionCell = "User.VsiComponentLayoutVersion";
    internal const string ChildIdsJsonCell = "User.VsiComponentChildIdsJson";
    internal const string ParentIdCell = "User.VsiComponentParentId";
    internal const string ChildPathCell = "User.VsiComponentChildPath";

    public IReadOnlyDictionary<string, string> BuildParentMetadata(
        CompositeComponentContract contract,
        CompositeComponentPlan plan)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(plan);
        plan.ValidateFor(contract);

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ComponentTypeCell] = contract.ComponentType,
            [SchemaVersionCell] = contract.SchemaVersion,
            [DataSha256Cell] = ComputeDataSha256(contract.Data),
            [LayoutVersionCell] = contract.LayoutVersion.ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            [ChildIdsJsonCell] = JsonSerializer.Serialize(
                plan.Primitives.Select(item => item.SemanticId),
                ProtocolJson.Options),
        };
    }

    public IReadOnlyDictionary<string, string> BuildChildMetadata(
        CompositeComponentContract contract,
        CompositePrimitivePlan primitive)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(primitive);
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ParentIdCell] = contract.SemanticId,
            [ChildPathCell] = primitive.ChildPath,
            [ComponentTypeCell] = contract.ComponentType,
            [SchemaVersionCell] = contract.SchemaVersion,
        };
    }

    internal static string ComputeDataSha256(JsonElement? data)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            if (data is null || data.Value.ValueKind is JsonValueKind.Null
                or JsonValueKind.Undefined)
            {
                writer.WriteNullValue();
            }
            else
            {
                WriteCanonicalJson(writer, data.Value);
            }
        }

        return Convert.ToHexString(SHA256.HashData(stream.ToArray()))
            .ToLowerInvariant();
    }

    private static void WriteCanonicalJson(Utf8JsonWriter writer, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in value.EnumerateObject()
                    .OrderBy(item => item.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonicalJson(writer, property.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in value.EnumerateArray())
                    WriteCanonicalJson(writer, item);
                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                writer.WriteStringValue(value.GetString());
                break;
            case JsonValueKind.Number:
                writer.WriteRawValue(value.GetRawText(), skipInputValidation: false);
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
                throw new BridgeCommandException(
                    BridgeErrorCodes.ComponentContractInvalid,
                    "组件数据包含不支持的 JSON 值。");
        }
    }
}
