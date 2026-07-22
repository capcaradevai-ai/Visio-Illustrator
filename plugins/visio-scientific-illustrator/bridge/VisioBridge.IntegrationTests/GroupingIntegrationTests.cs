using VisioBridge.Contracts;

namespace VisioBridge.IntegrationTests;

public sealed class GroupingIntegrationTests
{
    [Trait("Category", "VisioIntegration")]
    [Fact]
    public void Group_and_ungroup_preserve_member_semantic_ids()
    {
        using var fixture = VisioFixture.StartVisible();
        AddMemberShapes(fixture);

        fixture.GroupShapes(new GroupShapesCommand("pair", ["first", "second"]));

        var groupedIds = fixture.Inspect().Shapes
            .Select(shape => shape.SemanticId)
            .ToHashSet(StringComparer.Ordinal);
        Assert.Contains("pair", groupedIds);
        Assert.Contains("first", groupedIds);
        Assert.Contains("second", groupedIds);

        var result = fixture.UngroupShape(new UngroupShapeCommand("pair"));
        Assert.Equal(2, result.MemberCount);
        var ungroupedIds = fixture.Inspect().Shapes
            .Select(shape => shape.SemanticId)
            .ToHashSet(StringComparer.Ordinal);
        Assert.DoesNotContain("pair", ungroupedIds);
        Assert.Contains("first", ungroupedIds);
        Assert.Contains("second", ungroupedIds);
    }

    [Trait("Category", "VisioIntegration")]
    [Fact]
    public void Bundled_container_accepts_top_level_members()
    {
        using var fixture = VisioFixture.StartVisible();
        fixture.AddShape(new AddShapeCommand(
            "panel",
            "container",
            new PixelBox(80, 80, 900, 500),
            "Panel"));
        AddMemberShapes(fixture);

        var result = fixture.AddToContainer(new AddToContainerCommand(
            "panel",
            ["first", "second"]));

        Assert.Equal(2, result.VerifiedMemberCount);
    }

    private static void AddMemberShapes(VisioFixture fixture)
    {
        fixture.AddShape(new AddShapeCommand(
            "first",
            "rounded_rectangle",
            new PixelBox(180, 180, 240, 120),
            "First"));
        fixture.AddShape(new AddShapeCommand(
            "second",
            "ellipse",
            new PixelBox(560, 180, 240, 120),
            "Second"));
    }
}
