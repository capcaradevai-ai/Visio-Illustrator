using VisioBridge.Core;
using VisioBridge.Core.Export;
using VisioBridge.Tests.Fakes;

namespace VisioBridge.Tests;

public sealed class PreviewExporterTests
{
    [Fact]
    public void Preview_restores_raster_settings_when_export_fails()
    {
        var adapter = FakeExportAdapter.ThrowingWithSettings(800, 600);
        var exporter = new PreviewExporter(adapter, Path.GetTempPath());

        var error = Assert.Throws<BridgeCommandException>(() => exporter.Export(2000));

        Assert.Equal(BridgeErrorCodes.ExportFailed, error.Code);
        Assert.Equal((800, 600), (adapter.CurrentSize.WidthPx, adapter.CurrentSize.HeightPx));
        Assert.Equal(1, adapter.RestoreCount);
    }
}
