using VisioBridge.Core.Abstractions;

namespace VisioBridge.Tests.Fakes;

internal sealed class FakeExportAdapter : IPreviewExportAdapter
{
    private readonly bool _throwOnExport;

    private FakeExportAdapter(RasterSettings settings, bool throwOnExport)
    {
        CurrentSize = settings;
        _throwOnExport = throwOnExport;
    }

    public RasterSettings CurrentSize { get; private set; }
    public int ExportCount { get; private set; }
    public int RestoreCount { get; private set; }

    public static FakeExportAdapter ThrowingWithSettings(int width, int height) =>
        new(new RasterSettings(width, height, 96, 90), true);

    public RasterSettings ReadSettings() => CurrentSize;

    public void ApplyPreviewSettings(int widthPx)
    {
        CurrentSize = CurrentSize with { WidthPx = widthPx };
    }

    public void ExportPage(string absolutePngPath)
    {
        ExportCount++;
        if (_throwOnExport) throw new IOException("injected export failure");
    }

    public void RestoreSettings(RasterSettings settings)
    {
        CurrentSize = settings;
        RestoreCount++;
    }
}
