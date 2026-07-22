namespace VisioBridge.IntegrationTests;

public sealed class PreviewIntegrationTests
{
    [Trait("Category", "VisioIntegration")]
    [Fact]
    public void Preview_exports_decodable_2000_pixel_png_and_restores_settings()
    {
        using var fixture = VisioFixture.WithOneShape();
        var before = fixture.ReadRasterSettings();

        var preview = fixture.ExportPreview(2000);

        Assert.Equal(2000, preview.Width);
        Assert.True(preview.Bytes > 1000);
        Assert.Equal(before, fixture.ReadRasterSettings());
        Assert.True(File.Exists(preview.AbsolutePath));
    }
}
