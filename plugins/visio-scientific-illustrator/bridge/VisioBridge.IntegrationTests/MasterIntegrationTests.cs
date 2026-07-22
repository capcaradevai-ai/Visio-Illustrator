using VisioBridge.Contracts;
using VisioBridge.Core.Files;

namespace VisioBridge.IntegrationTests;

public sealed class MasterIntegrationTests
{
    [Trait("Category", "VisioIntegration")]
    [Fact]
    public void Every_bundled_master_survives_save_and_reopen_with_semantic_id()
    {
        var shapeTypes = new[]
        {
            "rounded_rectangle",
            "diamond",
            "cylinder",
            "hexagon",
            "triangle",
            "parallelogram",
            "cloud",
            "container",
        };
        var directory = Path.Combine(
            Path.GetTempPath(),
            "visio-master-integration",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var target = Path.Combine(directory, "masters.vsdx");
        try
        {
            using (var fixture = VisioFixture.StartVisible())
            {
                for (var index = 0; index < shapeTypes.Length; index++)
                {
                    try
                    {
                        fixture.AddShape(new AddShapeCommand(
                            $"master-{index}",
                            shapeTypes[index],
                            new PixelBox(
                                80 + index % 4 * 360,
                                100 + index / 4 * 360,
                                260,
                                180),
                            shapeTypes[index]));
                    }
                    catch (Exception error)
                    {
                        throw new InvalidOperationException(
                            $"Master integration failed for {shapeTypes[index]}.",
                            error);
                    }
                }
                fixture.Save(target);
            }

            using var fileService = new VisioFileModeService();
            var inspection = fileService.Inspect(new InspectFileCommand(target, 500));
            var semanticIds = inspection.Pages
                .SelectMany(page => page.Shapes)
                .Select(shape => shape.SemanticId)
                .ToHashSet(StringComparer.Ordinal);

            for (var index = 0; index < shapeTypes.Length; index++)
            {
                Assert.Contains($"master-{index}", semanticIds);
            }
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }
}
