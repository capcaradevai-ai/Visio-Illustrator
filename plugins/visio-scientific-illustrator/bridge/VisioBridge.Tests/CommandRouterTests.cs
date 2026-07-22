using System.Text.Json;
using VisioBridge.Core;
using VisioBridge.Core.Commands;
using VisioBridge.Core.Session;

namespace VisioBridge.Tests;

public sealed class CommandRouterTests
{
    [Fact]
    public void Unknown_command_returns_stable_unsupported_error()
    {
        using var session = new VisioSession();
        var router = new CommandRouter(session);
        using var document = JsonDocument.Parse("{}");

        var error = Assert.Throws<BridgeCommandException>(() =>
            router.Route("does_not_exist", document.RootElement));

        Assert.Equal(BridgeErrorCodes.UnsupportedOperation, error.Code);
    }
}
