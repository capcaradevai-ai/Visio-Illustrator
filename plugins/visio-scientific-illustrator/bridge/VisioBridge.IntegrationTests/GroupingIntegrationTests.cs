using VisioBridge.Contracts;

namespace VisioBridge.IntegrationTests;

public sealed class GroupingIntegrationTests
{
    [Trait("Category", "VisioIntegration")]
    [Fact]
    public void Position_only_update_uses_page_coordinates_for_a_group_member()
    {
        using var fixture = VisioFixture.StartVisible();
        AddMemberShapes(fixture);
        fixture.GroupShapes(new GroupShapesCommand("pair", ["first", "second"]));
        var before = fixture.Inspect().Shapes.ToDictionary(
            shape => shape.SemanticId,
            StringComparer.Ordinal);

        var updated = fixture.UpdateShape(new UpdateShapeCommand("first", X: 150));
        var after = fixture.Inspect().Shapes.ToDictionary(
            shape => shape.SemanticId,
            StringComparer.Ordinal);

        Assert.InRange(updated.Bounds.X, 149.5, 150.5);
        Assert.InRange(updated.Bounds.Y, before["first"].Bounds.Y - 0.5, before["first"].Bounds.Y + 0.5);
        Assert.InRange(updated.Bounds.Width, before["first"].Bounds.Width - 0.5, before["first"].Bounds.Width + 0.5);
        Assert.InRange(updated.Bounds.Height, before["first"].Bounds.Height - 0.5, before["first"].Bounds.Height + 0.5);
        Assert.InRange(after["second"].Bounds.X, before["second"].Bounds.X - 0.5, before["second"].Bounds.X + 0.5);
        Assert.InRange(after["second"].Bounds.Y, before["second"].Bounds.Y - 0.5, before["second"].Bounds.Y + 0.5);
        Assert.InRange(after["pair"].Bounds.X, 0, 1600);
        Assert.InRange(after["pair"].Bounds.Y, 0, 1000);
        Assert.InRange(after["pair"].Bounds.Width, 1, 1600);
        Assert.InRange(after["pair"].Bounds.Height, 1, 1000);
    }

    [Trait("Category", "VisioIntegration")]
    [Fact]
    public void Duplicate_group_semantic_id_returns_stable_error_and_rolls_back_grouping()
    {
        using var fixture = VisioFixture.StartVisible();
        AddMemberShapes(fixture);

        var error = Assert.Throws<VisioBridge.Core.BridgeCommandException>(() =>
            fixture.GroupShapes(new GroupShapesCommand(
                "first",
                ["first", "second"])));

        Assert.Equal(VisioBridge.Core.BridgeErrorCodes.DuplicateSemanticId, error.Code);
        Assert.Equal(2, fixture.GetStatus().ShapeCount);
        Assert.Equal(
            new[] { "first", "second" },
            fixture.Inspect().Shapes
                .Select(shape => shape.SemanticId)
                .Order(StringComparer.Ordinal));
    }

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
