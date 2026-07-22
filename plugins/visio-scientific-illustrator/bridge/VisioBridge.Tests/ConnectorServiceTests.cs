using VisioBridge.Contracts;
using VisioBridge.Core;
using VisioBridge.Tests.Fakes;
using System.Runtime.InteropServices;

namespace VisioBridge.Tests;

public sealed class ConnectorServiceTests
{
    [Fact]
    public void Connector_rejects_missing_source()
    {
        var service = ConnectorFixture.CreateWithoutShapes();

        var error = Assert.Throws<BridgeCommandException>(() => service.Add(
            new AddConnectorCommand("flow", "missing", "target")));

        Assert.Equal(BridgeErrorCodes.ShapeNotFound, error.Code);
    }

    [Fact]
    public void Connector_registers_after_both_endpoints_are_verified()
    {
        var fixture = ConnectorFixture.CreateWithShapes("source", "target");

        var result = fixture.Service.Add(new AddConnectorCommand(
            "flow",
            "source",
            "target"));

        Assert.Equal(2, result.ConnectionCount);
        Assert.Equal("Codex_flow", result.NameU);
        Assert.True(fixture.Adapter.GlueCalled);
    }

    [Fact]
    public void Polyline_connector_forwards_label_points_and_connection_rows()
    {
        var fixture = ConnectorFixture.CreateWithShapes("source", "target");
        var waypoints = new[] { new PixelPoint(300, 200), new PixelPoint(500, 400) };

        fixture.Service.Add(new AddConnectorCommand(
            "flow",
            "source",
            "target",
            Waypoints: waypoints,
            Label: "signal",
            Routing: "polyline",
            SourceConnectionPoint: 1,
            TargetConnectionPoint: 2));

        Assert.Equal("polyline", fixture.Adapter.Routing);
        Assert.Same(waypoints, fixture.Adapter.Waypoints);
        Assert.Equal("signal", fixture.Adapter.Label);
        Assert.Equal(1, fixture.Adapter.SourceConnectionPoint);
        Assert.Equal(2, fixture.Adapter.TargetConnectionPoint);
    }

    [Fact]
    public void Waypoints_require_polyline_routing()
    {
        var fixture = ConnectorFixture.CreateWithShapes("source", "target");

        var error = Assert.Throws<BridgeCommandException>(() => fixture.Service.Add(
            new AddConnectorCommand(
                "flow",
                "source",
                "target",
                Waypoints: [new PixelPoint(300, 200)])));

        Assert.Equal(BridgeErrorCodes.InvalidArgument, error.Code);
    }

    [Fact]
    public void Self_loop_connector_is_rejected_before_mutation()
    {
        var fixture = ConnectorFixture.CreateWithShapes("same");

        var error = Assert.Throws<BridgeCommandException>(() => fixture.Service.Add(
            new AddConnectorCommand("loop", "same", "same")));

        Assert.Equal(BridgeErrorCodes.InvalidArgument, error.Code);
    }

    [Fact]
    public void Connector_preserves_retryable_Visio_busy_errors()
    {
        var fixture = ConnectorFixture.CreateWithShapes("source", "target");
        fixture.Adapter.CreateError = new COMException(
            "busy",
            unchecked((int)0x8001010A));

        var error = Assert.Throws<BridgeCommandException>(() => fixture.Service.Add(
            new AddConnectorCommand("flow", "source", "target")));

        Assert.Equal(BridgeErrorCodes.VisioBusy, error.Code);
        Assert.True(error.Retryable);
    }
}
