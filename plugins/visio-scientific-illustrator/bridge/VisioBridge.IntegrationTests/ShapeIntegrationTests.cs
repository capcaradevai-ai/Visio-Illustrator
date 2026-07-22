using VisioBridge.Contracts;

namespace VisioBridge.IntegrationTests;

public sealed class ShapeIntegrationTests
{
    [Trait("Category", "VisioIntegration")]
    [Fact]
    public void Duplicate_semantic_id_returns_stable_error_without_a_partial_shape()
    {
        using var fixture = VisioFixture.StartVisible();
        fixture.AddShape(new AddShapeCommand(
            "encoder",
            "rectangle",
            new PixelBox(100, 100, 240, 100),
            "First"));

        var error = Assert.Throws<VisioBridge.Core.BridgeCommandException>(() =>
            fixture.AddShape(new AddShapeCommand(
                "encoder",
                "ellipse",
                new PixelBox(500, 100, 240, 100),
                "Duplicate")));

        Assert.Equal(VisioBridge.Core.BridgeErrorCodes.DuplicateSemanticId, error.Code);
        Assert.Single(fixture.Inspect().Shapes);
    }

    [Trait("Category", "VisioIntegration")]
    [Fact]
    public void Add_rectangle_creates_an_editable_semantic_shape()
    {
        using var fixture = VisioFixture.StartVisible();

        var result = fixture.AddShape(new AddShapeCommand(
            "encoder",
            "rectangle",
            new PixelBox(100, 100, 240, 100),
            "Encoder",
            new ShapeStyle(
                "#DAE8FC",
                0,
                "#6C8EBF",
                1.5,
                "#1F2937",
                14,
                0,
                false)));

        Assert.Equal("Codex_encoder", result.NameU);
        Assert.Equal("encoder", result.SemanticId);
        Assert.Equal(1, fixture.GetStatus().ShapeCount);
    }
}
