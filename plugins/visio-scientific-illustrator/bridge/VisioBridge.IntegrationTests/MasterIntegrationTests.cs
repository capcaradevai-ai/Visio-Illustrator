using VisioBridge.Contracts;
using VisioBridge.Core.Files;

namespace VisioBridge.IntegrationTests;

public sealed class MasterIntegrationTests
{
    [Trait("Category", "VisioIntegration")]
    [Fact]
    public void Cylinder_and_cloud_masters_are_composite_geometry_not_single_ellipses()
    {
        using var fixture = VisioFixture.StartVisible();
        fixture.AddShape(new AddShapeCommand(
            "cylinder", "cylinder", new PixelBox(100, 100, 350, 240), "Cylinder"));
        fixture.AddShape(new AddShapeCommand(
            "cloud", "cloud", new PixelBox(600, 100, 420, 240), "Cloud"));

        var cylinderChildren = fixture.ReadGroupChildCount("cylinder");
        var cloudChildren = fixture.ReadGroupChildCount("cloud");
        Assert.True(cylinderChildren >= 3);
        Assert.True(cloudChildren >= 5);
        Assert.NotEqual(cylinderChildren, cloudChildren);
    }

    [Trait("Category", "VisioIntegration")]
    [Fact]
    public void Every_parameterized_component_survives_save_and_reopen_with_editable_children()
    {
        var components = new[]
        {
            "number_badge", "router", "human_silhouette", "joint_skeleton",
            "video_frame_stack", "tensor_cube", "latent_vector", "waveform",
            "snowflake", "wireless_link", "stage_panel",
        };
        var target = Path.Combine(Path.GetTempPath(), $"visio-components-{Guid.NewGuid():N}.vsdx");
        try
        {
            using (var fixture = VisioFixture.StartVisible())
            {
                for (var index = 0; index < components.Length; index++)
                {
                    var result = fixture.AddScientificComponent(new AddScientificComponentCommand(
                        $"component-{index}",
                        components[index],
                        new PixelBox(30 + index % 4 * 390, 30 + index / 4 * 310, 320, 250),
                        new ScientificComponentOptions(
                            Title: components[index], Label: (index + 1).ToString(),
                            Rows: 4, Columns: 5, Count: 3)));
                    Assert.True(result.PrimitiveCount >= 3);
                    Assert.Equal(result.PrimitiveCount, result.ChildSemanticIds.Count);
                    Assert.Contains("left", result.ConnectionPoints);
                    Assert.Contains("right", result.ConnectionPoints);
                }
                fixture.Save(target);
            }

            using var fileService = new VisioFileModeService();
            var inspection = fileService.Inspect(new InspectFileCommand(target, 1000));
            var ids = inspection.Pages.SelectMany(page => page.Shapes)
                .Select(shape => shape.SemanticId).ToHashSet(StringComparer.Ordinal);
            for (var index = 0; index < components.Length; index++)
                Assert.Contains($"component-{index}", ids);
            Assert.True(ids.Count > components.Length * 3);
        }
        finally
        {
            if (File.Exists(target)) File.Delete(target);
        }
    }

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
            "tensor_face",
            "video_frame",
            "latent_vector",
            "snowflake",
            "router",
            "antenna",
            "stage_panel",
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
                                50 + index % 5 * 300,
                                70 + index / 5 * 280,
                                230,
                                150),
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
