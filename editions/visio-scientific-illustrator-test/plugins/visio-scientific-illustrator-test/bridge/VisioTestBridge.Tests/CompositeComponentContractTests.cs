using System.Text.Json;
using VisioTestBridge.Contracts;
using VisioTestBridge.Core;
using VisioTestBridge.Core.Components;

namespace VisioTestBridge.Tests;

public sealed class CompositeComponentContractTests
{
    [Fact]
    public void Contract_serializes_the_common_component_fields_with_protocol_names()
    {
        var contract = CreateContract();

        var json = JsonSerializer.SerializeToElement(contract, ProtocolJson.Options);

        Assert.Equal("accuracy-chart", json.GetProperty("semantic_id").GetString());
        Assert.Equal("chart", json.GetProperty("component_type").GetString());
        Assert.Equal("results", json.GetProperty("region_id").GetString());
        Assert.Equal(12, json.GetProperty("expected_revision").GetInt64());
        Assert.Equal("#2F75B5", json.GetProperty("style").GetProperty("primary_color").GetString());
    }

    [Fact]
    public void Pure_plan_preserves_order_and_generates_stable_hierarchical_child_ids()
    {
        var contract = CreateContract();
        var definitions = new[]
        {
            Primitive("axis.x", "path", new PixelBox(430, 230, 280, 1)),
            Primitive("series.method-a.point.1", "shape", new PixelBox(500, 160, 12, 12)),
            Primitive("legend.method-a", "shape", new PixelBox(650, 75, 14, 14)),
        };
        var connectionPoints = new[]
        {
            new CompositeConnectionPointPlan("left", new PixelPoint(420, 170)),
            new CompositeConnectionPointPlan("right", new PixelPoint(740, 170)),
        };

        var first = CompositeComponentPlan.Create(contract, definitions, connectionPoints);
        var second = CompositeComponentPlan.Create(contract, definitions, connectionPoints);

        Assert.Equal(
            [
                "accuracy-chart.axis.x",
                "accuracy-chart.series.method-a.point.1",
                "accuracy-chart.legend.method-a",
            ],
            first.Primitives.Select(item => item.SemanticId));
        Assert.Equal(first.Primitives, second.Primitives);
        Assert.Equal(contract.Bounds, first.ExpectedBounds);
        Assert.Equal(connectionPoints, first.ConnectionPoints);
        Assert.All(first.Primitives, primitive =>
            Assert.Equal(contract.SemanticId, primitive.ParentSemanticId));
    }

    [Theory]
    [InlineData("")]
    [InlineData(".axis")]
    [InlineData("axis.")]
    [InlineData("axis..x")]
    [InlineData("axis x")]
    public void Invalid_child_path_is_rejected_before_execution(string childPath)
    {
        var error = Assert.Throws<BridgeCommandException>(() =>
            CompositeComponentPlan.Create(
                CreateContract(),
                [Primitive(childPath, "path", new PixelBox(430, 230, 280, 1))]));

        Assert.Equal(BridgeErrorCodes.ComponentContractInvalid, error.Code);
    }

    [Fact]
    public void Parent_metadata_contains_required_cells_and_a_canonical_data_hash()
    {
        var first = CreateContract(data: JsonSerializer.SerializeToElement(
            new Dictionary<string, object?>
            {
                ["series"] = new[] { 1, 2, 3 },
                ["title"] = "Accuracy",
            }));
        var reordered = CreateContract(data: JsonSerializer.SerializeToElement(
            new Dictionary<string, object?>
            {
                ["title"] = "Accuracy",
                ["series"] = new[] { 1, 2, 3 },
            }));
        var firstPlan = CreatePlan(first);
        var reorderedPlan = CreatePlan(reordered);
        var service = new ComponentMetadataService();

        var firstMetadata = service.BuildParentMetadata(first, firstPlan);
        var reorderedMetadata = service.BuildParentMetadata(reordered, reorderedPlan);

        Assert.Equal("chart", firstMetadata["User.VsiComponentType"]);
        Assert.Equal("1.0", firstMetadata["User.VsiComponentSchemaVersion"]);
        Assert.Equal("1", firstMetadata["User.VsiComponentLayoutVersion"]);
        Assert.Equal(
            "[\"accuracy-chart.axis.x\",\"accuracy-chart.series.method-a.point.1\"]",
            firstMetadata["User.VsiComponentChildIdsJson"]);
        Assert.Matches("^[0-9a-f]{64}$", firstMetadata["User.VsiComponentDataSha256"]);
        Assert.Equal(
            firstMetadata["User.VsiComponentDataSha256"],
            reorderedMetadata["User.VsiComponentDataSha256"]);
    }

    [Fact]
    public void Successful_execution_commits_one_undo_scope_and_advances_revision_once()
    {
        var contract = CreateContract();
        var plan = CreatePlan(contract);
        var context = new FakeExecutionContext(contract.ExpectedRevision);
        var scopes = new FakeUndoScopeFactory(context);
        var executor = new CompositeComponentExecutor(
            context,
            scopes,
            new ComponentMetadataService());

        var result = executor.Execute(contract, plan);

        Assert.Equal(1, scopes.BeginCount);
        Assert.Equal(1, scopes.CompleteCount);
        Assert.Equal(1, scopes.DisposeCount);
        Assert.Equal(1, context.AdvanceRevisionCalls);
        Assert.Equal(contract.ExpectedRevision + 1, context.CurrentRevision);
        Assert.Equal(contract.ExpectedRevision, result.PreviousRevision);
        Assert.Equal(contract.ExpectedRevision + 1, result.NewRevision);
        Assert.Equal(plan.Primitives.Select(item => item.SemanticId), result.ChildSemanticIds);
        Assert.Contains(contract.SemanticId, context.Objects);
        Assert.All(plan.Primitives, primitive => Assert.Contains(primitive.SemanticId, context.Objects));
        Assert.Equal(
            "chart",
            context.Metadata[contract.SemanticId]["User.VsiComponentType"]);
        Assert.True(
            context.Operations.IndexOf($"group:{contract.SemanticId}")
            > context.Operations.IndexOf($"create:{plan.Primitives[^1].SemanticId}"));
        Assert.True(
            context.Operations.IndexOf($"metadata:{contract.SemanticId}")
            > context.Operations.IndexOf($"group:{contract.SemanticId}"));
    }

    [Fact]
    public void Failed_execution_rolls_back_all_primitives_metadata_group_and_revision()
    {
        var contract = CreateContract();
        var plan = CreatePlan(contract);
        var context = new FakeExecutionContext(contract.ExpectedRevision)
        {
            FailOnCreateCall = 2,
        };
        var scopes = new FakeUndoScopeFactory(context);
        var executor = new CompositeComponentExecutor(
            context,
            scopes,
            new ComponentMetadataService());

        Assert.Throws<InvalidOperationException>(() => executor.Execute(contract, plan));

        Assert.Empty(context.Objects);
        Assert.Empty(context.Metadata);
        Assert.Equal(contract.ExpectedRevision, context.CurrentRevision);
        Assert.Equal(0, context.AdvanceRevisionCalls);
        Assert.Equal(0, scopes.CompleteCount);
        Assert.Equal(1, scopes.DisposeCount);
    }

    [Fact]
    public void Revision_conflict_is_rejected_without_starting_an_undo_scope()
    {
        var contract = CreateContract();
        var plan = CreatePlan(contract);
        var context = new FakeExecutionContext(contract.ExpectedRevision + 1);
        var scopes = new FakeUndoScopeFactory(context);
        var executor = new CompositeComponentExecutor(
            context,
            scopes,
            new ComponentMetadataService());

        var error = Assert.Throws<BridgeCommandException>(() =>
            executor.Execute(contract, plan));

        Assert.Equal(BridgeErrorCodes.RevisionConflict, error.Code);
        Assert.Equal(0, scopes.BeginCount);
        Assert.Empty(context.Objects);
        Assert.Equal(0, context.AdvanceRevisionCalls);
    }

    private static CompositeComponentContract CreateContract(JsonElement? data = null) =>
        new(
            "accuracy-chart",
            "chart",
            "results",
            new PixelBox(420, 60, 320, 220),
            JsonSerializer.SerializeToElement(new { primary_color = "#2F75B5" }),
            12,
            data ?? JsonSerializer.SerializeToElement(new { title = "Accuracy" }));

    private static CompositeComponentPlan CreatePlan(CompositeComponentContract contract) =>
        CompositeComponentPlan.Create(
            contract,
            [
                Primitive("axis.x", "path", new PixelBox(430, 230, 280, 1)),
                Primitive("series.method-a.point.1", "shape", new PixelBox(500, 160, 12, 12)),
            ],
            [
                new CompositeConnectionPointPlan("left", new PixelPoint(420, 170)),
                new CompositeConnectionPointPlan("right", new PixelPoint(740, 170)),
            ]);

    private static CompositePrimitiveDefinition Primitive(
        string childPath,
        string kind,
        PixelBox bounds) =>
        new(
            childPath,
            kind,
            bounds,
            JsonSerializer.SerializeToElement(new { editable = true }));

    private sealed class FakeExecutionContext : ICompositeComponentExecutionContext
    {
        public FakeExecutionContext(long revision)
        {
            CurrentRevision = revision;
        }

        public long CurrentRevision { get; private set; }

        public int AdvanceRevisionCalls { get; private set; }

        public int? FailOnCreateCall { get; init; }

        public HashSet<string> Objects { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, IReadOnlyDictionary<string, string>> Metadata { get; } =
            new(StringComparer.Ordinal);

        public List<string> Operations { get; } = [];

        public void CreatePrimitive(CompositePrimitivePlan primitive)
        {
            Operations.Add($"create:{primitive.SemanticId}");
            Objects.Add(primitive.SemanticId);
            if (Operations.Count(item => item.StartsWith("create:", StringComparison.Ordinal))
                == FailOnCreateCall)
            {
                throw new InvalidOperationException("injected primitive failure");
            }
        }

        public void WriteMetadata(
            string semanticId,
            IReadOnlyDictionary<string, string> metadata)
        {
            Operations.Add($"metadata:{semanticId}");
            Metadata[semanticId] = metadata;
        }

        public void GroupComponent(
            string semanticId,
            IReadOnlyList<string> childSemanticIds)
        {
            Operations.Add($"group:{semanticId}");
            if (childSemanticIds.Any(child => !Objects.Contains(child)))
                throw new InvalidOperationException("cannot group missing child");
            Objects.Add(semanticId);
        }

        public void SetConnectionPoints(
            string semanticId,
            IReadOnlyList<CompositeConnectionPointPlan> connectionPoints)
        {
            Operations.Add($"connection-points:{semanticId}");
            if (!Objects.Contains(semanticId))
                throw new InvalidOperationException("cannot annotate missing component");
        }

        public void AdvanceRevision()
        {
            Operations.Add("advance-revision");
            AdvanceRevisionCalls++;
            CurrentRevision++;
        }

        public Snapshot Capture() =>
            new(
                CurrentRevision,
                AdvanceRevisionCalls,
                Objects.ToArray(),
                Metadata.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal),
                Operations.ToArray());

        public void Restore(Snapshot snapshot)
        {
            CurrentRevision = snapshot.Revision;
            AdvanceRevisionCalls = snapshot.AdvanceRevisionCalls;
            Objects.Clear();
            Objects.UnionWith(snapshot.Objects);
            Metadata.Clear();
            foreach (var item in snapshot.Metadata) Metadata.Add(item.Key, item.Value);
            Operations.Clear();
            Operations.AddRange(snapshot.Operations);
        }

        public sealed record Snapshot(
            long Revision,
            int AdvanceRevisionCalls,
            IReadOnlyList<string> Objects,
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Metadata,
            IReadOnlyList<string> Operations);
    }

    private sealed class FakeUndoScopeFactory : ICompositeComponentUndoScopeFactory
    {
        private readonly FakeExecutionContext _context;

        public FakeUndoScopeFactory(FakeExecutionContext context)
        {
            _context = context;
        }

        public int BeginCount { get; private set; }

        public int CompleteCount { get; private set; }

        public int DisposeCount { get; private set; }

        public ICompositeComponentUndoScope Begin(string name)
        {
            Assert.Equal("Create composite component accuracy-chart", name);
            BeginCount++;
            return new FakeUndoScope(this, _context, _context.Capture());
        }

        private sealed class FakeUndoScope : ICompositeComponentUndoScope
        {
            private readonly FakeUndoScopeFactory _owner;
            private readonly FakeExecutionContext _context;
            private readonly FakeExecutionContext.Snapshot _snapshot;
            private bool _completed;
            private bool _disposed;

            public FakeUndoScope(
                FakeUndoScopeFactory owner,
                FakeExecutionContext context,
                FakeExecutionContext.Snapshot snapshot)
            {
                _owner = owner;
                _context = context;
                _snapshot = snapshot;
            }

            public void Complete()
            {
                _completed = true;
                _owner.CompleteCount++;
            }

            public void Dispose()
            {
                if (_disposed) return;
                if (!_completed) _context.Restore(_snapshot);
                _owner.DisposeCount++;
                _disposed = true;
            }
        }
    }
}
