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
    public void Polyline_connector_forwards_named_anchors_to_creation_before_glue()
    {
        var fixture = ConnectorFixture.CreateWithShapes("source", "target");
        var sourceAnchor = new ConnectorAnchor("connection_point", Name: "lan_1");
        var targetAnchor = new ConnectorAnchor("connection_point", Name: "lan_2");

        fixture.Service.Add(new AddConnectorCommand(
            "named-polyline",
            "source",
            "target",
            Waypoints: [new PixelPoint(400, 300)],
            Routing: "polyline",
            SourceAnchor: sourceAnchor,
            TargetAnchor: targetAnchor));

        Assert.Equal(sourceAnchor, fixture.Adapter.CreatedSourceAnchor);
        Assert.Equal(targetAnchor, fixture.Adapter.CreatedTargetAnchor);
        Assert.Equal("named-polyline", fixture.Adapter.CreatedConnectorSemanticId);
        Assert.True(fixture.Adapter.GlueCalled);
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

    [Fact]
    public void Rich_connector_options_are_resolved_and_forwarded_atomically()
    {
        var fixture = ConnectorFixture.CreateWithShapes("source", "target", "obstacle");
        var sourceAnchor = new ConnectorAnchor("normalized", X: 1, Y: 0.25);
        var targetAnchor = new ConnectorAnchor("connection_point", Name: "left");

        fixture.Service.Add(new AddConnectorCommand(
            "flow",
            "source",
            "target",
            Label: "features",
            Routing: "right_angle",
            SourceAnchor: sourceAnchor,
            TargetAnchor: targetAnchor,
            JumpStyle: "arc",
            LabelPosition: 0.7,
            AvoidShapeIds: ["obstacle"]));

        Assert.Equal(sourceAnchor, fixture.Adapter.SourceAnchor);
        Assert.Equal(targetAnchor, fixture.Adapter.TargetAnchor);
        Assert.Equal("flow", fixture.Adapter.ConnectorSemanticId);
        Assert.Equal("arc", fixture.Adapter.JumpStyle);
        Assert.Equal(0.7, fixture.Adapter.LabelPosition);
        Assert.Equal(["obstacle"], fixture.Adapter.AvoidShapeIds);
    }

    [Fact]
    public void New_anchor_contract_cannot_be_mixed_with_legacy_connection_point_fields()
    {
        var fixture = ConnectorFixture.CreateWithShapes("source", "target");

        var error = Assert.Throws<BridgeCommandException>(() => fixture.Service.Add(
            new AddConnectorCommand(
                "flow",
                "source",
                "target",
                SourceConnectionPoint: 1,
                SourceAnchor: new ConnectorAnchor("auto"))));

        Assert.Equal(BridgeErrorCodes.InvalidArgument, error.Code);
    }

    [Theory]
    [InlineData("round")]
    [InlineData("")]
    public void Invalid_jump_style_is_rejected(string jumpStyle)
    {
        var fixture = ConnectorFixture.CreateWithShapes("source", "target");

        var error = Assert.Throws<BridgeCommandException>(() => fixture.Service.Add(
            new AddConnectorCommand("flow", "source", "target", JumpStyle: jumpStyle)));

        Assert.Equal(BridgeErrorCodes.InvalidArgument, error.Code);
    }

    [Fact]
    public void Label_position_and_avoid_shape_ids_are_validated_before_create()
    {
        var fixture = ConnectorFixture.CreateWithShapes("source", "target");

        var positionError = Assert.Throws<BridgeCommandException>(() => fixture.Service.Add(
            new AddConnectorCommand("flow", "source", "target", LabelPosition: 1.1)));
        var avoidError = Assert.Throws<BridgeCommandException>(() => fixture.Service.Add(
            new AddConnectorCommand("flow", "source", "target", AvoidShapeIds: ["missing"])));

        Assert.Equal(BridgeErrorCodes.InvalidArgument, positionError.Code);
        Assert.Equal(BridgeErrorCodes.ShapeNotFound, avoidError.Code);
    }
}
