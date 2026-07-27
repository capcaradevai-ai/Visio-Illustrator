using VisioTestBridge.Contracts;
using VisioTestBridge.Tests.Fakes;

namespace VisioTestBridge.Tests;

public sealed class PageInspectorTests
{
    [Fact]
    public void Update_label_does_not_change_geometry_or_style()
    {
        var fixture = FakePageFixture.WithShape(
            "encoder",
            label: "Old",
            x: 100,
            fill: "#DAE8FC");

        fixture.Editor.Update(new UpdateShapeCommand("encoder", Label: "New"));

        Assert.Equal("New", fixture.Shape.Label);
        Assert.Equal(100, fixture.Shape.Bounds.X);
        Assert.Equal("#DAE8FC", fixture.Shape.Style.FillColor);
    }

    [Fact]
    public void Update_one_geometry_field_preserves_the_other_fields()
    {
        var fixture = FakePageFixture.WithShape(
            "encoder",
            label: "Old",
            x: 100,
            fill: "#DAE8FC");

        fixture.Editor.Update(new UpdateShapeCommand("encoder", Width: 320));

        Assert.Equal(new PixelBox(100, 100, 320, 80), fixture.Shape.Bounds);
    }

    [Fact]
    public void Clear_requires_explicit_confirmation()
    {
        var fixture = FakePageFixture.WithShape(
            "encoder",
            label: "Old",
            x: 100,
            fill: "#DAE8FC");

        var error = Assert.Throws<VisioTestBridge.Core.BridgeCommandException>(
            () => fixture.Editor.Clear(confirm: false));

        Assert.Equal(VisioTestBridge.Core.BridgeErrorCodes.InvalidArgument, error.Code);
        Assert.False(fixture.Shape.Deleted);
    }
}
