using VisioBridge.Contracts;

namespace VisioBridge.IntegrationTests;

public sealed class LayoutIntegrationTests
{
    [Trait("Category", "VisioIntegration")]
    [Fact]
    public void Native_layout_layer_protection_z_order_and_duplicate_are_editable()
    {
        using var fixture = VisioFixture.StartVisible();
        AddShapes(fixture);

        fixture.AlignShapes(new AlignShapesCommand(["a", "b"], "top", "a"));
        fixture.DistributeShapes(new DistributeShapesCommand(
            ["a", "b", "c"], "horizontal", "equal_gap"));
        fixture.MatchSize(new MatchSizeCommand(["a", "b"], "a", "both"));
        fixture.AssignLayer(new AssignLayerCommand(["a", "b"], "scientific-content"));
        fixture.SetProtection(new SetProtectionCommand(
            ["a"],
            new ProtectionFields(MoveX: true, TextEdit: false, Aspect: true)));
        var zOrder = fixture.SetZOrder(new SetZOrderCommand("a", "bring_to_front"));
        var duplicate = fixture.DuplicateShape(new DuplicateShapeCommand(
            "a", "a-copy", 30, 40));

        var byId = fixture.Inspect().Shapes.ToDictionary(
            shape => shape.SemanticId,
            StringComparer.Ordinal);
        Assert.InRange(byId["b"].Bounds.Y, byId["a"].Bounds.Y - 0.75, byId["a"].Bounds.Y + 0.75);
        Assert.InRange(byId["b"].Bounds.Width, byId["a"].Bounds.Width - 0.75, byId["a"].Bounds.Width + 0.75);
        Assert.InRange(byId["b"].Bounds.Height, byId["a"].Bounds.Height - 0.75, byId["a"].Bounds.Height + 0.75);
        Assert.Contains("scientific-content", byId["a"].Layers);
        Assert.Equal(1, fixture.ReadCellResult("a", "LockMoveX"));
        Assert.Equal(1, fixture.ReadCellResult("a", "LockAspect"));
        Assert.True(zOrder.Changes[0].AfterZOrder >= zOrder.Changes[0].BeforeZOrder);
        Assert.InRange(byId["a-copy"].Bounds.X, byId["a"].Bounds.X + 29.25, byId["a"].Bounds.X + 30.75);
        Assert.InRange(byId["a-copy"].Bounds.Y, byId["a"].Bounds.Y + 39.25, byId["a"].Bounds.Y + 40.75);
        Assert.Equal("duplicate_shape", duplicate.Operation);
    }

    [Trait("Category", "VisioIntegration")]
    [Fact]
    public void Aligning_a_group_member_uses_page_coordinates()
    {
        using var fixture = VisioFixture.StartVisible();
        AddShapes(fixture);
        fixture.GroupShapes(new GroupShapesCommand("pair", ["a", "b"]));

        fixture.AlignShapes(new AlignShapesCommand(["a", "c"], "left", "c"));
        var duplicated = fixture.DuplicateShape(new DuplicateShapeCommand(
            "pair", "pair-copy", 40, 40));

        var byId = fixture.Inspect().Shapes.ToDictionary(
            shape => shape.SemanticId,
            StringComparer.Ordinal);
        Assert.InRange(byId["a"].Bounds.X, byId["c"].Bounds.X - 0.75, byId["c"].Bounds.X + 0.75);
        Assert.Contains("pair-copy", byId.Keys);
        Assert.Equal(2, duplicated.ChildSemanticIdMap?.Count);
        Assert.All(duplicated.ChildSemanticIdMap!.Values, id => Assert.Contains(id, byId.Keys));
    }

    private static void AddShapes(VisioFixture fixture)
    {
        fixture.AddShape(new AddShapeCommand("a", "rectangle", new PixelBox(100, 120, 180, 100), "A"));
        fixture.AddShape(new AddShapeCommand("b", "ellipse", new PixelBox(430, 260, 140, 140), "B"));
        fixture.AddShape(new AddShapeCommand("c", "rounded_rectangle", new PixelBox(850, 480, 220, 120), "C"));
    }
}
