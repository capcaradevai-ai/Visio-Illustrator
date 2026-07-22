using VisioBridge.Core;
using VisioBridge.Core.Files;

namespace VisioBridge.Tests;

public sealed class FileExportPolicyTests
{
    [Theory]
    [InlineData("png", "Page.Export")]
    [InlineData("svg", "Page.Export")]
    [InlineData("pdf", "Document.ExportAsFixedFormat")]
    public void Export_format_uses_expected_Visio_API(string format, string expected)
    {
        Assert.Equal(expected, FileExportPolicy.SelectApi(format));
    }

    [Fact]
    public void Unknown_export_format_is_rejected()
    {
        var error = Assert.Throws<BridgeCommandException>(
            () => FileExportPolicy.SelectApi("jpg"));

        Assert.Equal(BridgeErrorCodes.UnsupportedOperation, error.Code);
    }
}
