using VisioBridge.Contracts;
using VisioBridge.Core;
using VisioBridge.Tests.Fakes;

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
}
