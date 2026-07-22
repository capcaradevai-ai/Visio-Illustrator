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

        var beforeMove = fixture.Inspect().Shapes.Single(
            shape => shape.SemanticId == "flow");
        fixture.UpdateShape(new UpdateShapeCommand(
            "target",
            X: 900,
            Y: 420));
        var afterMove = fixture.Inspect().Shapes.Single(
            shape => shape.SemanticId == "flow");

        Assert.NotEqual(beforeMove.Bounds, afterMove.Bounds);
        Assert.Equal(
            new[] { "source", "target" },
            afterMove.ConnectedTo.Order(StringComparer.Ordinal));
        Assert.Equal(1, fixture.GetStatus().ConnectorCount);
    }

    [Trait("Category", "VisioIntegration")]
    [Fact]
    public void Polyline_connector_keeps_waypoints_label_and_glued_endpoints()
    {
        using var fixture = VisioFixture.StartVisible();
        fixture.AddShape(new AddShapeCommand(
            "poly-source",
            "rectangle",
            new PixelBox(100, 150, 200, 100),
            "Source"));
        fixture.AddShape(new AddShapeCommand(
            "poly-target",
            "rectangle",
            new PixelBox(1100, 650, 200, 100),
            "Target"));

        var result = fixture.AddConnector(new AddConnectorCommand(
            "poly-flow",
            "poly-source",
            "poly-target",
            Waypoints:
            [
                new PixelPoint(450, 700),
                new PixelPoint(850, 250),
            ],
            Label: "signal",
            Routing: "polyline"));

        var inspected = fixture.Inspect().Shapes.Single(
            shape => shape.SemanticId == "poly-flow");
        Assert.Equal(2, result.ConnectionCount);
        Assert.Equal("signal", inspected.Label);
        Assert.Equal(
            new[] { "poly-source", "poly-target" },
            inspected.ConnectedTo.Order(StringComparer.Ordinal));
        Assert.True(inspected.Bounds.Height > 300);
    }

    [Trait("Category", "VisioIntegration")]
    [Fact]
    public void Connector_can_glue_to_explicit_connection_points()
    {
        using var fixture = VisioFixture.StartVisible();
        fixture.AddShape(new AddShapeCommand(
            "point-source",
            "rectangle",
            new PixelBox(100, 250, 200, 100)));
        fixture.AddShape(new AddShapeCommand(
            "point-target",
            "rectangle",
            new PixelBox(800, 250, 200, 100)));
        fixture.AddConnectionPoint("point-source", 1, 0.5);
        fixture.AddConnectionPoint("point-target", 0, 0.5);

        var result = fixture.AddConnector(new AddConnectorCommand(
            "point-flow",
            "point-source",
            "point-target",
            SourceConnectionPoint: 1,
            TargetConnectionPoint: 1));

        Assert.Equal(2, result.ConnectionCount);
    }
}
