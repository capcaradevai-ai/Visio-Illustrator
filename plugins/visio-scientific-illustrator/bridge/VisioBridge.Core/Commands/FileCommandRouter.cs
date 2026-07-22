using System.Text.Json;
using VisioBridge.Contracts;
using VisioBridge.Core.Files;

namespace VisioBridge.Core.Commands;

public sealed class FileCommandRouter
{
    private readonly VisioFileModeService _service;

    public FileCommandRouter(VisioFileModeService service) => _service = service;

    public object Route(string command, JsonElement parameters) => command switch
    {
        "validate" => _service.Validate(Deserialize<ValidateFileCommand>(parameters)),
        "inspect_file" => _service.Inspect(Deserialize<InspectFileCommand>(parameters)),
        "export" => _service.Export(Deserialize<ExportFileCommand>(parameters)),
        _ => throw BridgeCommandException.Unsupported($"file mode 不支持命令: {command}"),
    };

    private static T Deserialize<T>(JsonElement parameters) =>
        parameters.Deserialize<T>(ProtocolJson.Options)
        ?? throw new BridgeCommandException(
            BridgeErrorCodes.InvalidArgument,
            "命令参数不能为空。");
}
