using System.Runtime.InteropServices;
using System.Reflection;
using System.Text.Json;
using VisioTestBridge.Contracts;
using VisioTestBridge.Core;
using VisioTestBridge.Core.Commands;
using VisioTestBridge.Core.Files;
using VisioTestBridge.Core.Interop;
using VisioTestBridge.Core.Session;

var mode = ParseMode(args);
if (mode is null)
{
    Console.Error.WriteLine("Usage: VisioTestBridge.exe --mode live|file");
    Environment.ExitCode = 2;
    return;
}

Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.InputEncoding = System.Text.Encoding.UTF8;
var bridgeVersion = Assembly.GetExecutingAssembly()
    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
    .InformationalVersion.Split('+')[0]
    ?? throw new InvalidOperationException("Bridge informational version is missing.");
Console.WriteLine(JsonSerializer.Serialize(
    new BridgeReady(
        "ready",
        bridgeVersion,
        "test",
        "0.4.0-test.1",
        Environment.ProcessId,
        RuntimeInformation.ProcessArchitecture.ToString()),
    ProtocolJson.Options));

using IDisposable owner = mode == "live"
    ? new VisioSession()
    : new VisioFileModeService();
Func<string, JsonElement, object> route = mode == "live"
    ? new CommandRouter((VisioSession)owner).Route
    : new FileCommandRouter((VisioFileModeService)owner).Route;

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
            route(request.Command, request.Params));
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
    catch (COMException error)
    {
        var translated = ComExceptionTranslator.Translate(error);
        response = new BridgeResponse(responseId, false, Error: new BridgeFailure(
            translated.Code,
            translated.Message,
            translated.Retryable,
            translated.Details));
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

static string? ParseMode(string[] arguments)
{
    if (arguments.Length != 2 || arguments[0] != "--mode") return null;
    return arguments[1] is "live" or "file" ? arguments[1] : null;
}
