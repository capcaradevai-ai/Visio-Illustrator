using VisioBridge.Contracts;

namespace VisioBridge.IntegrationTests;

public sealed class EditingIntegrationTests
{
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
