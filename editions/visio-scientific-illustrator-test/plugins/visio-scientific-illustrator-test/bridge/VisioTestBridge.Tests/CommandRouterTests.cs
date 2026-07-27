using System.Text.Json;
using VisioTestBridge.Core;
using VisioTestBridge.Core.Commands;
using VisioTestBridge.Core.Session;

namespace VisioTestBridge.Tests;

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

    [Theory]
    [InlineData("add_table", """
        {"component":{"semantic_id":"t","component_type":"table","region_id":"main",
        "bounds":{"x":0,"y":0,"width":100,"height":60},"expected_revision":0},
        "cells":[[{"text":"A"}]],"layout":{}}
        """)]
    [InlineData("add_chart", """
        {"component":{"semantic_id":"c","component_type":"chart","region_id":"main",
        "bounds":{"x":0,"y":0,"width":200,"height":120},"expected_revision":0},
        "chart_type":"bar","series":[{"name":"s","x":[0],"y":[1]}],
        "x_axis":{"label":"x"},"y_axis":{"label":"y"},"legend":{}}
        """)]
    [InlineData("add_matrix", """
        {"component":{"semantic_id":"m","component_type":"matrix","region_id":"main",
        "bounds":{"x":0,"y":0,"width":100,"height":60},"expected_revision":0},
        "values":[[1]]}
        """)]
    [InlineData("add_grid", """
        {"component":{"semantic_id":"g","component_type":"grid","region_id":"main",
        "bounds":{"x":0,"y":0,"width":100,"height":60},"expected_revision":0},
        "rows":1,"columns":1}
        """)]
    public void Composite_commands_are_routed_to_the_live_session(
        string command,
        string json)
    {
        using var session = new VisioSession();
        var router = new CommandRouter(session);
        using var document = JsonDocument.Parse(json);

        var error = Assert.Throws<BridgeCommandException>(() =>
            router.Route(command, document.RootElement));

        Assert.Equal(BridgeErrorCodes.SessionNotReady, error.Code);
    }
}
