using VisioBridge.Contracts;
using VisioBridge.Core.Validation;

namespace VisioBridge.Tests;

public sealed class FidelityValidatorTests
{
    [Fact]
    public void Matching_scene_text_budget_and_glue_pass()
    {
        var result = new FidelityValidator().Validate(Scene(), Inspection(), 0.9);

        Assert.True(result.Valid);
        Assert.Empty(result.Errors);
        Assert.Equal(1, result.Metrics.GluedConnectors);
    }

    [Fact]
    public void Missing_text_and_single_ended_connector_fail_independently_of_visual_similarity()
    {
        var inspection = Inspection() with
        {
            Shapes =
            [
                Shape("a", "Wrong"),
                Shape("flow", "", "connector", ["a"]),
            ],
            ShapeCount = 1,
            ConnectorCount = 1,
        };

        var result = new FidelityValidator().Validate(Scene(), inspection, 0.9);

        Assert.False(result.Valid);
        Assert.Contains(result.Errors, error => error.Contains("文字不匹配", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains("未双端粘附", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains("缺少 semantic_id", StringComparison.Ordinal));
    }

    private static ScientificScene Scene() => new(
        "scene", new SceneCanvas(800, 500, 96), new Dictionary<string, string>(), ["content"],
        [
            new SceneShapeNode("a", new PixelBox(50, 50, 100, 60), "rectangle", Label: "A"),
            new SceneShapeNode("b", new PixelBox(500, 50, 100, 60), "ellipse", Label: "B"),
        ],
        [new SceneConnector("flow", "a", "b", "straight", true)],
        [new SceneRegion("main", new PixelBox(0, 0, 800, 500), true)]);

    private static PageInspectionResult Inspection() => new(
        "doc", "page", 1, 2, 1, false,
        [Shape("a", "A"), Shape("b", "B"), Shape("flow", "", "connector", ["a", "b"])],
        false);

    private static ShapeSnapshot Shape(
        string id,
        string label,
        string type = "shape",
        IReadOnlyList<string>? connected = null) => new(
            id, id.GetHashCode(), $"Codex_{id}", type, label,
            new PixelBox(0, 0, 100, 50), 0, null, [], connected ?? []);
}
