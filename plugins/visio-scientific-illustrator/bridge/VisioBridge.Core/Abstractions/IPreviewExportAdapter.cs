namespace VisioBridge.Core.Abstractions;

public sealed record RasterSettings(
    int WidthPx,
    int HeightPx,
    int Resolution,
    int Quality,
    int SizeMode = 2,
    int SizeUnits = 0,
    double ResolutionWidth = 96,
    double ResolutionHeight = 96,
    int ResolutionUnits = 0,
    int ColorFormat = 0,
    uint BackgroundColor = 0);

public sealed record PreviewResult(
    string AbsolutePath,
    int Width,
    int Height,
    long Bytes);

public interface IPreviewExportAdapter
{
    RasterSettings ReadSettings();

    void ApplyPreviewSettings(int widthPx);

    void ExportPage(string absolutePngPath);

    void RestoreSettings(RasterSettings settings);
}
