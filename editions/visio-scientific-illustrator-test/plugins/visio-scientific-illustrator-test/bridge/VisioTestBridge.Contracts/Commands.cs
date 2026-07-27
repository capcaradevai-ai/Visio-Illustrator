namespace VisioTestBridge.Contracts;

public sealed record StatusResult(string State);

public sealed record LaunchNewCommand(
    [property: System.Text.Json.Serialization.JsonPropertyName("page_name")] string PageName,
    [property: System.Text.Json.Serialization.JsonPropertyName("width_px")] int WidthPx,
    [property: System.Text.Json.Serialization.JsonPropertyName("height_px")] int HeightPx,
    int Dpi,
    bool Maximize);

public sealed record OpenDocumentCommand(
    [property: System.Text.Json.Serialization.JsonPropertyName("file_path")] string FilePath,
    [property: System.Text.Json.Serialization.JsonPropertyName("page_name")] string? PageName,
    int Dpi,
    bool Maximize);

public sealed record ShutdownCommand(
    bool Confirm,
    [property: System.Text.Json.Serialization.JsonPropertyName("dirty_action")] string DirtyAction);
