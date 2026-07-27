namespace VisioTestBridge.Core.Session;

public enum SessionState
{
    Stopped,
    Starting,
    Ready,
    Invalidated,
    Faulted,
}

public sealed record CanvasSpec(int WidthPx, int HeightPx, int Dpi);

public sealed record SessionStatus(
    SessionState State,
    int? ProcessId,
    bool Visible,
    string? DocumentName,
    string? PageName,
    int ShapeCount,
    int ConnectorCount,
    bool Dirty);

public sealed record LaunchResult(
    string SessionId,
    int ProcessId,
    bool Visible,
    string VisioVersion,
    string DocumentName,
    string PageName,
    CanvasSpec Canvas,
    string SessionTempRoot);
