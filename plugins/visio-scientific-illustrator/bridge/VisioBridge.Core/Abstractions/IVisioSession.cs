using VisioBridge.Contracts;
using VisioBridge.Core.Session;

namespace VisioBridge.Core.Abstractions;

public interface IVisioSession : IDisposable
{
    SessionState State { get; }

    int? OwnedProcessId { get; }

    SessionStatus GetStatus();

    ShapeMutationResult AddShape(AddShapeCommand command);

    ShapeMutationResult AddImage(AddImageCommand command);

    LaunchResult LaunchNew(
        string pageName,
        int widthPx,
        int heightPx,
        int dpi,
        bool maximize);

    LaunchResult Open(
        string absoluteVsdxPath,
        int widthPx,
        int heightPx,
        int dpi,
        bool maximize);

    void CloseDocument(string action);

    void Shutdown(bool confirm, string dirtyAction);
}
