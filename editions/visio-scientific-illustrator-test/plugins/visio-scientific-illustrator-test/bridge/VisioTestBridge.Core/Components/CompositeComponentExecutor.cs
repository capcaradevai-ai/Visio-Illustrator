using VisioTestBridge.Contracts;

namespace VisioTestBridge.Core.Components;

internal interface ICompositeComponentUndoScope : IDisposable
{
    void Complete();
}

internal interface ICompositeComponentUndoScopeFactory
{
    ICompositeComponentUndoScope Begin(string name);
}

internal interface ICompositeComponentExecutionContext
{
    long CurrentRevision { get; }

    void CreatePrimitive(CompositePrimitivePlan primitive);

    void WriteMetadata(
        string semanticId,
        IReadOnlyDictionary<string, string> metadata);

    void GroupComponent(
        string semanticId,
        IReadOnlyList<string> childSemanticIds);

    void SetConnectionPoints(
        string semanticId,
        IReadOnlyList<CompositeConnectionPointPlan> connectionPoints);

    void AdvanceRevision();
}

internal sealed class CompositeComponentExecutor
{
    private readonly ICompositeComponentExecutionContext _context;
    private readonly ICompositeComponentUndoScopeFactory _undoScopes;
    private readonly ComponentMetadataService _metadata;

    public CompositeComponentExecutor(
        ICompositeComponentExecutionContext context,
        ICompositeComponentUndoScopeFactory undoScopes,
        ComponentMetadataService metadata)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _undoScopes = undoScopes ?? throw new ArgumentNullException(nameof(undoScopes));
        _metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
    }

    public CompositeComponentMutationResult Execute(
        CompositeComponentContract contract,
        CompositeComponentPlan plan)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(plan);

        plan.ValidateFor(contract);
        var parentMetadata = _metadata.BuildParentMetadata(contract, plan);
        var childMetadata = plan.Primitives
            .Select(primitive => (
                Primitive: primitive,
                Metadata: _metadata.BuildChildMetadata(contract, primitive)))
            .ToArray();

        var previousRevision = _context.CurrentRevision;
        if (previousRevision != contract.ExpectedRevision)
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.RevisionConflict,
                $"页面修订号为 {previousRevision}，合同期望 {contract.ExpectedRevision}。",
                details: new
                {
                    current_revision = previousRevision,
                    expected_revision = contract.ExpectedRevision,
                });
        }

        using var undoScope = _undoScopes.Begin(
            $"Create composite component {contract.SemanticId}");
        foreach (var item in childMetadata)
            _context.CreatePrimitive(item.Primitive);
        foreach (var item in childMetadata)
            _context.WriteMetadata(item.Primitive.SemanticId, item.Metadata);

        var childIds = plan.Primitives
            .Select(item => item.SemanticId)
            .ToArray();
        _context.GroupComponent(contract.SemanticId, childIds);
        _context.SetConnectionPoints(contract.SemanticId, plan.ConnectionPoints);
        _context.WriteMetadata(contract.SemanticId, parentMetadata);
        _context.AdvanceRevision();
        undoScope.Complete();

        return new CompositeComponentMutationResult(
            contract.SemanticId,
            contract.ComponentType,
            childIds,
            previousRevision,
            previousRevision + 1);
    }
}
