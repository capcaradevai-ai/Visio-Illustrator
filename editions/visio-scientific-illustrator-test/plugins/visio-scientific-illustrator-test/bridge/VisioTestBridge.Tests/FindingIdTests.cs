using VisioTestBridge.Core.Quality;

namespace VisioTestBridge.Tests;

public sealed class FindingIdTests
{
    [Fact]
    public void Id_is_stable_across_revision_object_order_and_case()
    {
        var first = FindingId.Create("Connector_Glue", "Encoder", ["Node-B", "node-a"]);
        var second = FindingId.Create("connector_glue", "encoder", ["NODE-A", "node-b"]);

        Assert.Equal("connector_glue:encoder:node-a,node-b", first);
        Assert.Equal(first, second);
    }

    [Fact]
    public void Removes_duplicate_object_ids_before_building_identity()
    {
        var id = FindingId.Create(
            "unexpected_overlap",
            "encoder",
            ["Node-B", "node-a", "NODE-B"]);

        Assert.Equal("unexpected_overlap:encoder:node-a,node-b", id);
    }

    [Theory]
    [InlineData("", "encoder")]
    [InlineData("glue", "")]
    public void Rejects_empty_identity_parts(string category, string region)
    {
        Assert.Throws<ArgumentException>(() => FindingId.Create(category, region, ["node"]));
    }

    [Fact]
    public void Rejects_missing_object_identity()
    {
        Assert.Throws<ArgumentException>(
            () => FindingId.Create("glue", "encoder", []));
        Assert.Throws<ArgumentException>(
            () => FindingId.Create("glue", "encoder", ["node", " "]));
    }
}
