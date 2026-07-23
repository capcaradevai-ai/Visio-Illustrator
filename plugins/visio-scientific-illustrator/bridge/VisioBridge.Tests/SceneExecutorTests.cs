using System.Text.Json;
using VisioBridge.Contracts;
using VisioBridge.Core.Scenes;

namespace VisioBridge.Tests;

public sealed class SceneExecutorTests
{
    [Fact]
    public void Scene_plan_orders_content_labels_connectors_z_order_and_checkpoints()
    {
        var scene = new ScientificScene(
            "scene",
            new SceneCanvas(1000, 600, 96),
            new Dictionary<string, string>(),
            ["background", "content", "labels"],
            [
                new SceneTextNode("label", new PixelBox(400, 40, 180, 50), "Title", Layer: "labels", ZOrder: 9),
                new SceneShapeNode("background", new PixelBox(0, 0, 1000, 600), "rectangle", Layer: "background", Role: "background"),
                new SceneShapeNode("a", new PixelBox(100, 200, 120, 80), "rectangle", Layer: "content"),
                new SceneShapeNode("b", new PixelBox(700, 200, 120, 80), "ellipse", Layer: "content"),
            ],
            [new SceneConnector("flow", "a", "b", "straight")],
            [new SceneRegion("main", new PixelBox(0, 0, 1000, 600), true)]);

        var plan = SceneExecutor.Prepare(new DrawSceneCommand(scene));

        Assert.Equal("ensure_layers", plan.Steps[0].Command);
        Assert.True(IndexOf(plan, "add_shape", "background") < IndexOf(plan, "add_shape", "a"));
        Assert.True(IndexOf(plan, "add_connector", "flow") < IndexOf(plan, "add_shape", "label"));
        Assert.True(IndexOf(plan, "add_shape", "label") < IndexOf(plan, "set_z_order", "label"));
        Assert.True(plan.Steps[^1].CheckpointAfter);
    }

    [Fact]
    public void Checkpoints_finish_each_region_before_the_next_region_starts()
    {
        var scene = new ScientificScene(
            "regional", new SceneCanvas(1000, 500, 96), new Dictionary<string, string>(), ["content", "labels"],
            [
                new SceneShapeNode("left-a", new PixelBox(40, 100, 100, 80), "rectangle"),
                new SceneShapeNode("left-b", new PixelBox(260, 100, 100, 80), "rectangle"),
                new SceneTextNode("left-label", new PixelBox(100, 250, 180, 40), "Left", Layer: "labels"),
                new SceneShapeNode("right-a", new PixelBox(590, 100, 100, 80), "rectangle"),
                new SceneShapeNode("right-b", new PixelBox(810, 100, 100, 80), "rectangle"),
                new SceneTextNode("right-label", new PixelBox(650, 250, 180, 40), "Right", Layer: "labels"),
            ],
            [
                new SceneConnector("left-flow", "left-a", "left-b", "straight"),
                new SceneConnector("right-flow", "right-a", "right-b", "straight"),
            ],
            [
                new SceneRegion("left", new PixelBox(0, 0, 500, 500), true),
                new SceneRegion("right", new PixelBox(500, 0, 500, 500), true),
            ]);

        var plan = SceneExecutor.Prepare(new DrawSceneCommand(scene));
        var leftCheckpoint = plan.Steps.ToList().FindIndex(step => step.RegionId == "left" && step.CheckpointAfter);
        var firstRightStep = plan.Steps.ToList().FindIndex(step => step.RegionId == "right");

        Assert.True(leftCheckpoint >= 0);
        Assert.True(leftCheckpoint < firstRightStep);
        Assert.True(IndexOf(plan, "add_connector", "left-flow") < IndexOf(plan, "add_shape", "left-label"));
        Assert.True(IndexOf(plan, "add_connector", "right-flow") < IndexOf(plan, "add_shape", "right-label"));
    }

    [Fact]
    public void Region_range_filters_nodes_and_existing_ids_are_explicit_conflicts()
    {
        var scene = new ScientificScene(
            "scene", new SceneCanvas(1000, 500, 96), new Dictionary<string, string>(), ["content"],
            [
                new SceneShapeNode("left", new PixelBox(50, 50, 100, 100), "rectangle", Layer: "content"),
                new SceneShapeNode("right", new PixelBox(700, 50, 100, 100), "rectangle", Layer: "content"),
            ], [],
            [
                new SceneRegion("left-region", new PixelBox(0, 0, 500, 500), true),
                new SceneRegion("right-region", new PixelBox(500, 0, 500, 500), true),
            ]);

        var plan = SceneExecutor.Prepare(
            new DrawSceneCommand(scene, StartRegion: "right-region", EndRegion: "right-region"),
            ["right"]);

        Assert.DoesNotContain(plan.Steps, step => step.SemanticId == "left");
        Assert.Contains(plan.Conflicts, conflict => conflict.SemanticId == "right");
        Assert.DoesNotContain(plan.Steps, step => step.SemanticId == "right");
    }

    [Fact]
    public void Group_nodes_are_created_after_children_and_before_connectors()
    {
        var scene = new ScientificScene(
            "grouped", new SceneCanvas(800, 500, 96), new Dictionary<string, string>(), ["content"],
            [
                new SceneGroupNode("pair", new PixelBox(50, 50, 400, 200), Layer: "content"),
                new SceneShapeNode("a", new PixelBox(80, 80, 100, 80), "rectangle", ParentId: "pair"),
                new SceneShapeNode("b", new PixelBox(260, 80, 100, 80), "ellipse", ParentId: "pair"),
                new SceneShapeNode("target", new PixelBox(600, 80, 100, 80), "rectangle"),
            ],
            [new SceneConnector("flow", "pair", "target", "straight")],
            [new SceneRegion("main", new PixelBox(0, 0, 800, 500), true)]);

        var plan = SceneExecutor.Prepare(new DrawSceneCommand(scene));

        Assert.True(IndexOf(plan, "add_shape", "a") < IndexOf(plan, "group_shapes", "pair"));
        Assert.True(IndexOf(plan, "group_shapes", "pair") < IndexOf(plan, "add_connector", "flow"));
    }

    [Fact]
    public void Rich_text_shape_plan_does_not_send_mutually_exclusive_plain_label()
    {
        var richText = new RichText("InputSD", Runs:
        [
            new TextRun(0, 5, Bold: true),
            new TextRun(5, 2, Subscript: true),
        ]);
        var scene = new ScientificScene(
            "rich", new SceneCanvas(400, 200, 96), new Dictionary<string, string>(), ["content"],
            [new SceneShapeNode("shape", new PixelBox(20, 20, 200, 100), "rectangle", "InputSD", RichText: richText)],
            [], [new SceneRegion("main", new PixelBox(0, 0, 400, 200), true)]);

        var plan = SceneExecutor.Prepare(new DrawSceneCommand(scene));
        var step = plan.Steps.Single(item => item.SemanticId == "shape" && item.Command == "add_shape");
        var command = step.Parameters.Deserialize<AddShapeCommand>(ProtocolJson.Options)!;

        Assert.Null(command.Label);
        Assert.Equal(richText.Text, command.RichText!.Text);
    }

    private static int IndexOf(SceneExecutionPlan plan, string command, string id) =>
        plan.Steps.ToList().FindIndex(step => step.Command == command && step.SemanticId == id);
}
