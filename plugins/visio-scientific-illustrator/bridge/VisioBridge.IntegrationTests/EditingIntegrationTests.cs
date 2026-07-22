using VisioBridge.Contracts;

namespace VisioBridge.IntegrationTests;

public sealed class EditingIntegrationTests
{
    [Trait("Category", "VisioIntegration")]
    [Fact]
    public void Position_only_update_preserves_rotated_page_bounds()
    {
        using var fixture = VisioFixture.StartVisible();
        fixture.AddShape(new AddShapeCommand(
            "rotated",
            "rectangle",
            new PixelBox(100, 200, 300, 100),
            "Rotated",
            new ShapeStyle(RotationDeg: 45)));
        var before = Assert.Single(fixture.Inspect().Shapes);

        var updated = fixture.UpdateShape(new UpdateShapeCommand(
            "rotated",
            X: 400));

        Assert.InRange(updated.Bounds.X, 399.5, 400.5);
        Assert.InRange(updated.Bounds.Y, before.Bounds.Y - 0.5, before.Bounds.Y + 0.5);
        Assert.InRange(updated.Bounds.Width, before.Bounds.Width - 0.5, before.Bounds.Width + 0.5);
        Assert.InRange(updated.Bounds.Height, before.Bounds.Height - 0.5, before.Bounds.Height + 0.5);
        Assert.InRange(updated.RotationDeg, 44.9, 45.1);
    }

    [Trait("Category", "VisioIntegration")]
    [Fact]
    public void Label_only_update_preserves_latest_position_and_is_one_undo_step()
    {
        using var fixture = VisioFixture.StartVisible();
        fixture.AddShape(new AddShapeCommand(
            "encoder",
            "rectangle",
            new PixelBox(100, 200, 200, 100),
            "Old",
            new ShapeStyle(FillColor: "#DAE8FC")));
        fixture.UpdateShape(new UpdateShapeCommand("encoder", X: 420, Y: 360));

        fixture.UpdateShape(new UpdateShapeCommand("encoder", Label: "Backbone"));

        var changed = Assert.Single(fixture.Inspect().Shapes);
        Assert.Equal("Backbone", changed.Label);
        Assert.InRange(changed.Bounds.X, 419.5, 420.5);
        Assert.InRange(changed.Bounds.Y, 359.5, 360.5);

        fixture.Undo();
        var undone = Assert.Single(fixture.Inspect().Shapes);
        Assert.Equal("Old", undone.Label);
        Assert.InRange(undone.Bounds.X, 419.5, 420.5);
        Assert.InRange(undone.Bounds.Y, 359.5, 360.5);
    }
}
