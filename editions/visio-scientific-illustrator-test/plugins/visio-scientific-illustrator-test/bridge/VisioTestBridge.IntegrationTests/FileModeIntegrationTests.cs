using System.Security.Cryptography;
using VisioTestBridge.Contracts;
using VisioTestBridge.Core.Files;

namespace VisioTestBridge.IntegrationTests;

public sealed class FileModeIntegrationTests
{
    [Trait("Category", "VisioIntegration")]
    [Fact]
    public void File_mode_validates_inspects_and_exports_without_modifying_source()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "visio-file-mode-integration",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var source = Path.Combine(directory, "source.vsdx");
        try
        {
            using (var fixture = VisioFixture.StartVisible())
            {
                fixture.AddShape(new AddShapeCommand(
                    "file-shape",
                    "ellipse",
                    new PixelBox(200, 200, 400, 250),
                    "Editable"));
                fixture.Save(source);
            }
            var sourceHash = SHA256.HashData(File.ReadAllBytes(source));

            using var service = new VisioFileModeService();
            var validation = service.Validate(new ValidateFileCommand(source));
            var inspection = service.Inspect(new InspectFileCommand(source, 500));
            var pngPath = Path.Combine(directory, "output.png");
            File.WriteAllText(pngPath, "pre-existing output");
            var png = service.Export(new ExportFileCommand(
                source,
                "png",
                pngPath,
                "current",
                Overwrite: true,
                Width: 2000));
            var svg = service.Export(new ExportFileCommand(
                source,
                "svg",
                Path.Combine(directory, "output.svg"),
                "current"));
            var pdf = service.Export(new ExportFileCommand(
                source,
                "pdf",
                Path.Combine(directory, "output.pdf"),
                "all"));

            Assert.True(validation.Valid);
            Assert.True(png.Overwritten);
            Assert.Single(inspection.Pages);
            Assert.Contains(
                inspection.Pages[0].Shapes,
                shape => shape.SemanticId == "file-shape");
            Assert.True(png.Bytes > 1000);
            Assert.True(svg.Bytes > 100);
            Assert.True(pdf.Bytes > 1000);
            Assert.Equal(sourceHash, SHA256.HashData(File.ReadAllBytes(source)));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }
}
