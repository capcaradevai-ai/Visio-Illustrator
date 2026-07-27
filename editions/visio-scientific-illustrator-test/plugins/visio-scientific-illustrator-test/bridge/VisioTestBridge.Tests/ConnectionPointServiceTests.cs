using VisioTestBridge.Contracts;
using VisioTestBridge.Core;
using VisioTestBridge.Core.Shapes;

namespace VisioTestBridge.Tests;

public sealed class ConnectionPointServiceTests
{
    [Theory]
    [InlineData(10, 0, "right")]
    [InlineData(-10, 0, "left")]
    [InlineData(0, 10, "top")]
    [InlineData(0, -10, "bottom")]
    [InlineData(10, 10, "top_right")]
    [InlineData(-10, 10, "top_left")]
    [InlineData(10, -10, "bottom_right")]
    [InlineData(-10, -10, "bottom_left")]
    public void Auto_anchor_uses_the_nearest_semantic_side(double dx, double dy, string expected)
    {
        Assert.Equal(expected, ConnectionPointService.SelectAutoAnchor(dx, dy));
    }

    [Fact]
    public void Standard_anchor_definitions_use_top_down_normalized_coordinates()
    {
        var top = ConnectionPointService.StandardAnchors["top"];
        var bottomRight = ConnectionPointService.StandardAnchors["bottom_right"];

        Assert.Equal(new ConnectionPointDefinition(0.5, 0, 0, 1), top);
        Assert.Equal(new ConnectionPointDefinition(1, 1, 1, -1), bottomRight);
    }

    [Fact]
    public void Normalized_anchor_requires_a_complete_unit_square_point()
    {
        ConnectionPointService.ValidateAnchor(new ConnectorAnchor("normalized", X: 0.75, Y: 0.25));

        var missing = Assert.Throws<BridgeCommandException>(() =>
            ConnectionPointService.ValidateAnchor(new ConnectorAnchor("normalized", X: 0.5)));
        var outside = Assert.Throws<BridgeCommandException>(() =>
            ConnectionPointService.ValidateAnchor(new ConnectorAnchor("normalized", X: 1.2, Y: 0.5)));

        Assert.Equal(BridgeErrorCodes.InvalidArgument, missing.Code);
        Assert.Equal(BridgeErrorCodes.InvalidArgument, outside.Code);
    }

    [Fact]
    public void Explicit_connection_point_accepts_exactly_one_row_or_name()
    {
        ConnectionPointService.ValidateAnchor(new ConnectorAnchor("connection_point", Row: 3));
        ConnectionPointService.ValidateAnchor(new ConnectorAnchor("connection_point", Name: "right_port"));

        Assert.Throws<BridgeCommandException>(() => ConnectionPointService.ValidateAnchor(
            new ConnectorAnchor("connection_point", Row: 3, Name: "right_port")));
        Assert.Throws<BridgeCommandException>(() => ConnectionPointService.ValidateAnchor(
            new ConnectorAnchor("connection_point")));
    }
}
