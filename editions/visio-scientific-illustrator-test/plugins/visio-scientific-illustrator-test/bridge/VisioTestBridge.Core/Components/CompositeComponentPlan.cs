using System.Text.Json;
using System.Text.RegularExpressions;
using VisioTestBridge.Contracts;

namespace VisioTestBridge.Core.Components;

internal sealed record CompositeConnectionPointPlan(
    string Name,
    PixelPoint Position);

internal sealed record CompositePrimitiveDefinition(
    string ChildPath,
    string Kind,
    PixelBox Bounds,
    JsonElement Parameters);

internal sealed record CompositePrimitivePlan(
    string SemanticId,
    string ParentSemanticId,
    string ChildPath,
    string Kind,
    PixelBox Bounds,
    JsonElement Parameters);

internal sealed record CompositeComponentPlan(
    string ParentSemanticId,
    string ComponentType,
    PixelBox ExpectedBounds,
    IReadOnlyList<CompositePrimitivePlan> Primitives,
    IReadOnlyList<CompositeConnectionPointPlan> ConnectionPoints)
{
    private static readonly Regex SemanticIdPattern = new(
        @"^[A-Za-z0-9_-]+(?:\.[A-Za-z0-9_-]+)*$",
        RegexOptions.CultureInvariant);

    private static readonly HashSet<string> PrimitiveKinds = new(
        ["shape", "path", "image", "connector"],
        StringComparer.Ordinal);

    public static CompositeComponentPlan Create(
        CompositeComponentContract contract,
        IReadOnlyList<CompositePrimitiveDefinition> definitions,
        IReadOnlyList<CompositeConnectionPointPlan>? connectionPoints = null)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(definitions);
        ValidateContract(contract);
        if (definitions.Count == 0)
            throw Invalid("复合组件至少需要一个可编辑图元。");

        var childIds = new HashSet<string>(StringComparer.Ordinal);
        var primitives = new List<CompositePrimitivePlan>(definitions.Count);
        foreach (var definition in definitions)
        {
            ArgumentNullException.ThrowIfNull(definition);
            ValidateChildPath(definition.ChildPath);
            if (!PrimitiveKinds.Contains(definition.Kind))
                throw Invalid($"不支持的复合组件图元类型：{definition.Kind}");
            ValidateBounds(definition.Bounds, "primitive bounds");
            if (definition.Parameters.ValueKind is JsonValueKind.Undefined
                or JsonValueKind.Null)
            {
                throw Invalid($"图元 {definition.ChildPath} 必须提供执行参数。");
            }

            var semanticId = CreateChildSemanticId(contract.SemanticId, definition.ChildPath);
            if (!childIds.Add(semanticId))
                throw Invalid($"复合组件子 ID 重复：{semanticId}");
            primitives.Add(new CompositePrimitivePlan(
                semanticId,
                contract.SemanticId,
                definition.ChildPath,
                definition.Kind,
                definition.Bounds,
                definition.Parameters.Clone()));
        }

        var points = connectionPoints?.ToArray() ?? [];
        ValidateConnectionPoints(points);
        return new CompositeComponentPlan(
            contract.SemanticId,
            contract.ComponentType,
            contract.Bounds,
            primitives,
            points);
    }

    public static string CreateChildSemanticId(string parentSemanticId, string childPath)
    {
        ValidateSemanticId(parentSemanticId, "semantic_id");
        ValidateChildPath(childPath);
        return $"{parentSemanticId}.{childPath}";
    }

    internal static void ValidateContract(CompositeComponentContract contract)
    {
        ValidateSemanticId(contract.SemanticId, "semantic_id");
        if (string.IsNullOrWhiteSpace(contract.ComponentType))
            throw Invalid("component_type 不能为空。");
        if (string.IsNullOrWhiteSpace(contract.RegionId))
            throw Invalid("region_id 不能为空。");
        ValidateBounds(contract.Bounds, "bounds");
        if (contract.ExpectedRevision < 0)
            throw Invalid("expected_revision 不能小于零。");
        if (string.IsNullOrWhiteSpace(contract.SchemaVersion))
            throw Invalid("schema_version 不能为空。");
        if (contract.LayoutVersion <= 0)
            throw Invalid("layout_version 必须大于零。");
        ValidateJsonObject(contract.Style, "style");
        ValidateJsonObject(contract.Data, "data");
    }

    internal void ValidateFor(CompositeComponentContract contract)
    {
        ValidateContract(contract);
        if (!string.Equals(ParentSemanticId, contract.SemanticId, StringComparison.Ordinal))
            throw Invalid("计划父 ID 与合同 semantic_id 不一致。");
        if (!string.Equals(ComponentType, contract.ComponentType, StringComparison.Ordinal))
            throw Invalid("计划组件类型与合同 component_type 不一致。");
        if (ExpectedBounds != contract.Bounds)
            throw Invalid("计划边界与合同 bounds 不一致。");
        if (Primitives.Count == 0)
            throw Invalid("复合组件计划不包含图元。");

        var expectedIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var primitive in Primitives)
        {
            if (!string.Equals(
                    primitive.ParentSemanticId,
                    contract.SemanticId,
                    StringComparison.Ordinal))
            {
                throw Invalid($"图元 {primitive.SemanticId} 的父 ID 不正确。");
            }
            var expectedId = CreateChildSemanticId(contract.SemanticId, primitive.ChildPath);
            if (!string.Equals(primitive.SemanticId, expectedId, StringComparison.Ordinal))
                throw Invalid($"图元 ID 不符合稳定层级规则：{primitive.SemanticId}");
            if (!expectedIds.Add(primitive.SemanticId))
                throw Invalid($"复合组件子 ID 重复：{primitive.SemanticId}");
            if (!PrimitiveKinds.Contains(primitive.Kind))
                throw Invalid($"不支持的复合组件图元类型：{primitive.Kind}");
            ValidateBounds(primitive.Bounds, "primitive bounds");
            if (primitive.Parameters.ValueKind is JsonValueKind.Undefined
                or JsonValueKind.Null)
            {
                throw Invalid($"图元 {primitive.SemanticId} 必须提供执行参数。");
            }
        }
        ValidateConnectionPoints(ConnectionPoints);
    }

    private static void ValidateSemanticId(string value, string field)
    {
        if (string.IsNullOrWhiteSpace(value) || !SemanticIdPattern.IsMatch(value))
            throw Invalid($"{field} 必须使用非空的点号层级 ID。");
    }

    private static void ValidateChildPath(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !SemanticIdPattern.IsMatch(value))
            throw Invalid("子路径必须由非空的字母、数字、下划线或连字符片段组成，并以点号分层。");
    }

    private static void ValidateBounds(PixelBox bounds, string field)
    {
        if (!double.IsFinite(bounds.X)
            || !double.IsFinite(bounds.Y)
            || !double.IsFinite(bounds.Width)
            || !double.IsFinite(bounds.Height)
            || bounds.Width <= 0
            || bounds.Height <= 0)
        {
            throw Invalid($"{field} 必须是有限的正尺寸边界。");
        }
    }

    private static void ValidateJsonObject(JsonElement? value, string field)
    {
        if (value is not null
            && value.Value.ValueKind is not JsonValueKind.Object
            and not JsonValueKind.Null)
        {
            throw Invalid($"{field} 必须是 JSON 对象。");
        }
    }

    private static void ValidateConnectionPoints(
        IReadOnlyList<CompositeConnectionPointPlan> connectionPoints)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var point in connectionPoints)
        {
            ArgumentNullException.ThrowIfNull(point);
            if (string.IsNullOrWhiteSpace(point.Name) || !names.Add(point.Name))
                throw Invalid("连接点名称必须非空且唯一。");
            if (!double.IsFinite(point.Position.X) || !double.IsFinite(point.Position.Y))
                throw Invalid($"连接点 {point.Name} 必须使用有限坐标。");
        }
    }

    private static BridgeCommandException Invalid(string message) =>
        new(BridgeErrorCodes.ComponentContractInvalid, message);
}
