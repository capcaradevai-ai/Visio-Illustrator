using VisioBridge.Contracts;

namespace VisioBridge.IntegrationTests;

public sealed class SceneExecutionIntegrationTests
{
    [Trait("Category", "VisioIntegration")]
    [Fact]
    public void Prepared_scene_executes_as_independent_live_Visio_operations()
    {
        using var fixture = VisioFixture.StartVisible();
        var scene = new ScientificScene(
            "live-scene",
            new SceneCanvas(1600, 1000, 96),
            new Dictionary<string, string>(),
            ["background", "content", "labels"],
            [
                new SceneShapeNode(
                    "background", new PixelBox(20, 20, 1560, 960), "rounded_rectangle",
                    Style: new ShapeStyle(Fill: new FillStyle("solid", "#F7FAFD"), Line: new LineStyle("#B8C9DA", 1)),
                    Layer: "background", Role: "background"),
                new SceneShapeNode("source", new PixelBox(180, 350, 260, 140), "rounded_rectangle", Label: "Source", Layer: "content"),
                new ScenePathNode(
                    "wave", new PixelBox(700, 350, 300, 140),
                    [new ScenePathCommand("M", 0, 70), new ScenePathCommand("C", 300, 70, 60, 0, 220, 140)],
                    Style: new ShapeStyle(Fill: new FillStyle("none"), Line: new LineStyle("#245B91", 2)),
                    Layer: "content"),
                new SceneTextNode("title", new PixelBox(500, 80, 600, 70), "Progressive Scene", Layer: "labels", ZOrder: 3),
            ],
            [new SceneConnector("flow", "source", "wave", "straight")],
            [new SceneRegion("main", new PixelBox(0, 0, 1600, 1000), true)]);

        var plan = fixture.PrepareScene(new DrawSceneCommand(scene));
        Assert.Empty(plan.Conflicts);
        foreach (var step in plan.Steps)
        {
            fixture.Router.Route(step.Command, step.Parameters);
            fixture.Refresh();
        }

        var inspection = fixture.Inspect();
        var ids = inspection.Shapes.Select(shape => shape.SemanticId).ToHashSet(StringComparer.Ordinal);
        Assert.Contains("background", ids);
        Assert.Contains("source", ids);
        Assert.Contains("wave", ids);
        Assert.Contains("title", ids);
        Assert.Contains("flow", ids);
        Assert.Contains(inspection.Shapes, shape => shape.SemanticId == "flow" && shape.ConnectedTo.Count == 2);
        Assert.True(plan.Steps[^1].CheckpointAfter);

        var resumed = fixture.PrepareScene(new DrawSceneCommand(scene));
        Assert.NotEmpty(resumed.Conflicts);
        Assert.DoesNotContain(resumed.Steps, step => step.SemanticId is "source" or "wave" or "flow");
    }
}
