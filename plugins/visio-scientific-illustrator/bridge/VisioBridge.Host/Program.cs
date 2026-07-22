using System.Runtime.InteropServices;
using System.Text.Json;
using VisioBridge.Contracts;
using VisioBridge.Core;

Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.WriteLine(JsonSerializer.Serialize(
    new BridgeReady(
        "ready",
        "0.1.0",
        Environment.ProcessId,
        RuntimeInformation.ProcessArchitecture.ToString()),
    ProtocolJson.Options));

string? line;
while ((line = Console.ReadLine()) is not null)
{
    BridgeResponse response;
    try
    {
        var request = JsonSerializer.Deserialize<BridgeRequest>(
                ProtocolInput.NormalizeLine(line),
                ProtocolJson.Options)
            ?? throw new JsonException("请求为空。");
        response = request.Command == "status"
            ? new BridgeResponse(request.Id, true, new StatusResult("stopped"))
            : new BridgeResponse(request.Id, false, Error: new BridgeFailure(
                BridgeErrorCodes.UnsupportedOperation,
                $"不支持命令: {request.Command}",
                false));
    }
    catch (Exception error)
    {
        response = new BridgeResponse(0, false, Error: new BridgeFailure(
            BridgeErrorCodes.InvalidRequest,
            error.Message,
            false));
    }

    Console.WriteLine(JsonSerializer.Serialize(response, ProtocolJson.Options));
}
