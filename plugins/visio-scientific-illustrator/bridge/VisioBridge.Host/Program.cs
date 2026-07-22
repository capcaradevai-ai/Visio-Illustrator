using System.Runtime.InteropServices;
using System.Text.Json;
using VisioBridge.Contracts;
using VisioBridge.Core;
using VisioBridge.Core.Commands;
using VisioBridge.Core.Session;

Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.WriteLine(JsonSerializer.Serialize(
    new BridgeReady(
        "ready",
        "0.1.0",
        Environment.ProcessId,
        RuntimeInformation.ProcessArchitecture.ToString()),
    ProtocolJson.Options));

using var session = new VisioSession();
var router = new CommandRouter(session);

string? line;
while ((line = Console.ReadLine()) is not null)
{
    BridgeResponse response;
    long responseId = 0;
    try
    {
        var request = JsonSerializer.Deserialize<BridgeRequest>(
                ProtocolInput.NormalizeLine(line),
                ProtocolJson.Options)
            ?? throw new JsonException("请求为空。");
        responseId = request.Id;
        response = new BridgeResponse(
            request.Id,
            true,
            router.Route(request.Command, request.Params));
    }
    catch (BridgeCommandException error)
    {
        response = new BridgeResponse(responseId, false, Error: new BridgeFailure(
            error.Code,
            error.Message,
            error.Retryable,
            error.Details));
    }
    catch (JsonException error)
    {
        response = new BridgeResponse(responseId, false, Error: new BridgeFailure(
            BridgeErrorCodes.InvalidRequest,
            error.Message,
            false));
    }
    catch (Exception error)
    {
        response = new BridgeResponse(responseId, false, Error: new BridgeFailure(
            BridgeErrorCodes.InternalError,
            error.Message,
            false));
    }

    Console.WriteLine(JsonSerializer.Serialize(response, ProtocolJson.Options));
}
