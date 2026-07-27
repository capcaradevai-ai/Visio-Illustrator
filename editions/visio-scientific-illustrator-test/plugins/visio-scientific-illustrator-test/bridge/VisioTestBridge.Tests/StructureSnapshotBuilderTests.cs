using VisioTestBridge.Core.Quality;

namespace VisioTestBridge.Tests;

public sealed class StructureSnapshotBuilderTests
{
    [Fact]
    public void Hash_is_stable_across_input_order_and_subpixel_noise()
    {
        var first = new[]
        {
            Item("b", 20.123441, "#aabbcc"),
            Item("a", 10.987641, "#112233"),
        };
        var second = new[]
        {
            Item("a", 10.987639, "#112233"),
            Item("b", 20.123439, "#AABBCC"),
        };

        var left = StructureSnapshotBuilder.Build(first);
        var right = StructureSnapshotBuilder.Build(second);

        Assert.Equal(left.Sha256, right.Sha256);
        Assert.Contains("\"semantic_id\":\"a\"", left.CanonicalJson);
        Assert.True(left.CanonicalJson.IndexOf("\"semantic_id\":\"a\"", StringComparison.Ordinal)
            < left.CanonicalJson.IndexOf("\"semantic_id\":\"b\"", StringComparison.Ordinal));
        Assert.Contains("#AABBCC", left.CanonicalJson);
    }

    [Fact]
    public void Any_audit_relevant_field_change_changes_hash()
    {
        var baseline = StructureSnapshotBuilder.Build([Item("a", 10, "#112233")]);
        var changed = StructureSnapshotBuilder.Build([
            Item("a", 10, "#112233") with { Text = "changed" },
        ]);

        Assert.NotEqual(baseline.Sha256, changed.Sha256);
    }

    private static StructureSnapshotItem Item(string id, double x, string color) =>
        new(
            SemanticId: id,
            Kind: "shape",
            Bounds: new SnapshotBounds(x, 20, 100, 50),
            Text: "label",
            StyleSummary: new SnapshotStyle(color, "#445566", 1.25),
            GroupParent: null,
            Layer: "content",
            ZOrder: 2,
            ConnectorEndpoints: null,
            GlueState: null,
            ImageDeclarationHash: null,
            ComponentType: null,
            RegionId: "encoder",
            LastModifiedRevision: 7);
}
