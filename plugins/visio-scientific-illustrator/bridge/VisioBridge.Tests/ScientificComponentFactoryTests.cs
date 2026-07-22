using VisioBridge.Contracts;
using VisioBridge.Core.Shapes;

namespace VisioBridge.Tests;

public sealed class ScientificComponentFactoryTests
{
    public static IEnumerable<object[]> ComponentCases()
    {
        yield return ["number_badge", 3];
        yield return ["router", 7];
        yield return ["human_silhouette", 6];
        yield return ["joint_skeleton", 15];
        yield return ["video_frame_stack", 4];
        yield return ["tensor_cube", 10];
        yield return ["latent_vector", 5];
        yield return ["waveform", 3];
        yield return ["snowflake", 18];
        yield return ["wireless_link", 3];
        yield return ["stage_panel", 3];
    }

    [Theory]
    [MemberData(nameof(ComponentCases))]
    public void Every_scientific_component_has_editable_primitive_budget(
        string component,
        int minimumPrimitives)
    {
        var plan = ScientificComponentPlanner.Create(new AddScientificComponentCommand(
            "component",
            component,
            new PixelBox(100, 100, 600, 400),
            new ScientificComponentOptions(
                Title: "Stage",
                Label: "1",
                Rows: 4,
                Columns: 5,
                Count: 4)));

        Assert.True(plan.Primitives.Count >= minimumPrimitives);
        Assert.All(plan.Primitives, primitive =>
        {
            Assert.False(string.IsNullOrWhiteSpace(primitive.Suffix));
            Assert.True(primitive.Bounds.Width > 0);
            Assert.True(primitive.Bounds.Height > 0);
            Assert.Contains(primitive.Kind, new[] { "shape", "path", "image" });
        });
        Assert.Equal(plan.Primitives.Count, plan.Primitives.Select(item => item.Suffix).Distinct().Count());
    }

    [Fact]
    public void Tensor_dimensions_control_the_number_of_editable_grid_lines()
    {
        var plan = ScientificComponentPlanner.Create(new AddScientificComponentCommand(
            "tensor",
            "tensor_cube",
            new PixelBox(0, 0, 500, 400),
            new ScientificComponentOptions(Rows: 6, Columns: 7)));

        Assert.Equal(6, plan.Primitives.Count(item => item.Suffix.StartsWith("row_", StringComparison.Ordinal)));
        Assert.Equal(7, plan.Primitives.Count(item => item.Suffix.StartsWith("column_", StringComparison.Ordinal)));
    }

    [Fact]
    public void Invalid_component_or_unbounded_density_is_rejected()
    {
        Assert.Throws<VisioBridge.Core.BridgeCommandException>(() =>
            ScientificComponentPlanner.Create(new AddScientificComponentCommand(
                "x", "unknown", new PixelBox(0, 0, 100, 100))));
        Assert.Throws<VisioBridge.Core.BridgeCommandException>(() =>
            ScientificComponentPlanner.Create(new AddScientificComponentCommand(
                "x", "tensor_cube", new PixelBox(0, 0, 100, 100),
                new ScientificComponentOptions(Rows: 101, Columns: 2))));
    }
}
