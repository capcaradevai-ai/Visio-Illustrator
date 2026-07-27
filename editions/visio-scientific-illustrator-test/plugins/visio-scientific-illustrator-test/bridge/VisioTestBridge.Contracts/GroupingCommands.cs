using System.Text.Json.Serialization;

namespace VisioTestBridge.Contracts;

public sealed record GroupShapesCommand(
    [property: JsonPropertyName("group_semantic_id")] string GroupSemanticId,
    [property: JsonPropertyName("member_semantic_ids")] IReadOnlyList<string> MemberSemanticIds);

public sealed record UngroupShapeCommand(
    [property: JsonPropertyName("group_semantic_id")] string GroupSemanticId);

public sealed record AddToContainerCommand(
    [property: JsonPropertyName("container_semantic_id")] string ContainerSemanticId,
    [property: JsonPropertyName("member_semantic_ids")] IReadOnlyList<string> MemberSemanticIds);

public sealed record GroupMutationResult(
    string GroupSemanticId,
    int ShapeId,
    IReadOnlyList<string> MemberSemanticIds);

public sealed record UngroupMutationResult(
    string GroupSemanticId,
    int MemberCount);

public sealed record ContainerMutationResult(
    string ContainerSemanticId,
    IReadOnlyList<string> MemberSemanticIds,
    int VerifiedMemberCount);
