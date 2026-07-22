using System.Text.Json;
using VisioBridge.Contracts;
using VisioBridge.Core.Abstractions;

namespace VisioBridge.Core.Commands;

public sealed class CommandRouter
{
    private readonly IVisioSession _session;

    public CommandRouter(IVisioSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public object Route(string command, JsonElement parameters) => command switch
    {
        "launch_new" => LaunchNew(Deserialize<LaunchNewCommand>(parameters)),
        "open_document" => OpenDocument(Deserialize<OpenDocumentCommand>(parameters)),
        "status" => _session.GetStatus(),
        "add_shape" => _session.AddShape(Deserialize<AddShapeCommand>(parameters)),
        "add_image" => _session.AddImage(Deserialize<AddImageCommand>(parameters)),
        "add_connector" => _session.AddConnector(Deserialize<AddConnectorCommand>(parameters)),
        "inspect" => _session.Inspect(Deserialize<InspectCommand>(parameters).MaxShapes),
        "update_shape" => _session.UpdateShape(Deserialize<UpdateShapeCommand>(parameters)),
        "delete_shape" => _session.DeleteShape(Deserialize<DeleteShapeCommand>(parameters)),
        "clear" => _session.Clear(Deserialize<ClearCommand>(parameters).Confirm),
        "fit" => _session.Fit(Deserialize<FitCommand>(parameters).Mode),
        "preview" => _session.ExportPreview(Deserialize<PreviewCommand>(parameters).WidthPx),
        "close_document" => CloseDocument(Deserialize<CloseDocumentCommand>(parameters)),
        "shutdown" => Shutdown(Deserialize<ShutdownCommand>(parameters)),
        _ => throw BridgeCommandException.Unsupported($"不支持命令: {command}"),
    };

    private object LaunchNew(LaunchNewCommand command) => _session.LaunchNew(
        command.PageName,
        command.WidthPx,
        command.HeightPx,
        command.Dpi,
        command.Maximize);

    private object OpenDocument(OpenDocumentCommand command) => _session.Open(
        command.FilePath,
        command.WidthPx,
        command.HeightPx,
        command.Dpi,
        command.Maximize);

    private object Shutdown(ShutdownCommand command)
    {
        _session.Shutdown(command.Confirm, command.DirtyAction);
        return new { shutdown = _session.State == Session.SessionState.Stopped };
    }

    private object CloseDocument(CloseDocumentCommand command)
    {
        _session.CloseDocument(command.Action);
        return new { closed = _session.State == Session.SessionState.Stopped };
    }

    private static T Deserialize<T>(JsonElement parameters) =>
        parameters.Deserialize<T>(ProtocolJson.Options)
        ?? throw new BridgeCommandException(
            BridgeErrorCodes.InvalidArgument,
            "命令参数不能为空。");
}
