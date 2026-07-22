using VisioBridge.Contracts;

namespace VisioBridge.IntegrationTests;

public sealed class ConnectorIntegrationTests
{
    [Trait("Category", "VisioIntegration")]
    [Fact]
    public void Connector_is_glued_to_both_semantic_shapes()
    {
        using var fixture = VisioFixture.StartVisible();
        fixture.AddShape(new AddShapeCommand(
            "source",
            "rectangle",
            new PixelBox(100, 200, 200, 100),
            "Source"));
        fixture.AddShape(new AddShapeCommand(
            "target",
            "rectangle",
            new PixelBox(700, 200, 200, 100),
            "Target"));

        var connector = fixture.AddConnector(new AddConnectorCommand(
            "flow",
            "source",
            "target",
            new ConnectorStyle(
                StrokeColor: "#334155",
                StrokeWidthPt: 1.5,
                EndArrow: "triangle")));

        Assert.Equal(2, connector.ConnectionCount);
        Assert.Equal("Codex_flow", connector.NameU);
        Assert.Equal(1, fixture.GetStatus().ConnectorCount);
    }
}
