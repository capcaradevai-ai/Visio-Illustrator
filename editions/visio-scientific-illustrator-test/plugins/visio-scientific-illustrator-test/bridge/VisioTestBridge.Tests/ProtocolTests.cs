using System.Text.Json;
using VisioTestBridge.Contracts;
using VisioTestBridge.Core.Session;

namespace VisioTestBridge.Tests;

public sealed class ProtocolTests
{
    [Fact]
    public void Path_arc_uses_the_snake_case_wire_contract()
    {
        const string json = """
            {
              "semantic_id":"arc",
              "bounds":{"x":0,"y":0,"width":100,"height":100},
              "sections":[{"commands":[
                {"op":"M","x":0,"y":50},
                {"op":"A","x":100,"y":50,"rx":50,"ry":30,"rotation_deg":15,"large_arc":false,"sweep":true}
              ]}]
            }
            """;

        var command = JsonSerializer.Deserialize<AddPathCommand>(json, ProtocolJson.Options);

        Assert.NotNull(command);
        Assert.False(command.Sections[0].Commands[1].LargeArc);
        Assert.Equal(15, command.Sections[0].Commands[1].RotationDeg);
    }

    [Fact]
    public void Request_round_trips_with_command_and_id()
    {
        const string json = """{"id":7,"command":"status","params":{}}""";
        var request = JsonSerializer.Deserialize<BridgeRequest>(json, ProtocolJson.Options);

        Assert.NotNull(request);
        Assert.Equal(7, request.Id);
        Assert.Equal("status", request.Command);
    }

    [Fact]
    public void Protocol_input_removes_a_leading_utf8_bom()
    {
        const string line = "\uFEFF{\"id\":1}";

        Assert.Equal("{\"id\":1}", ProtocolInput.NormalizeLine(line));
    }

    [Fact]
    public void Command_dto_rejects_unknown_fields_at_the_bridge_boundary()
    {
        const string json = """
            {
              "page_name":"Figure",
              "width_px":1600,
              "height_px":1000,
              "dpi":96,
              "maximize":true,
              "extra":true
            }
            """;

        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<LaunchNewCommand>(json, ProtocolJson.Options));
    }

    [Fact]
    public void Session_state_serializes_as_a_camel_case_name()
    {
        var json = JsonSerializer.Serialize(
            new SessionStatus(SessionState.Stopped, null, false, null, null, 0, 0, false),
            ProtocolJson.Options);

        Assert.Contains("\"state\":\"stopped\"", json);
    }
}
