using System.Text.Json;
using VisioBridge.Contracts;
using VisioBridge.Core.Session;

namespace VisioBridge.Tests;

public sealed class ProtocolTests
{
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
