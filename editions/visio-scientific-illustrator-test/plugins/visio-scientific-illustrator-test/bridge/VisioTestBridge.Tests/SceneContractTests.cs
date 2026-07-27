using System.Text.Json;
using VisioTestBridge.Contracts;

namespace VisioTestBridge.Tests;

public sealed class SceneContractTests
{
    private const string ValidSceneJson = """
        {
          "scene_id":"scene",
          "canvas":{"width_px":1600,"height_px":900,"dpi":96},
          "style_tokens":{"blue":"#0B4A8B"},
          "layers":["content","connectors"],
          "nodes":[
            {
              "kind":"shape",
              "semantic_id":"input",
              "bounds":{"x":10,"y":20,"width":100,"height":60},
              "layer":"content",
              "shape":"rectangle"
            },
            {
              "kind":"path",
              "semantic_id":"wave",
              "bounds":{"x":200,"y":100,"width":120,"height":60},
              "layer":"content",
              "commands":[
                {"op":"M","x":0,"y":30},
                {"op":"L","x":120,"y":30}
              ]
            }
          ],
          "connectors":[
            {"semantic_id":"flow","source":"input","target":"wave","routing":"straight"}
          ],
          "regions":[
            {"id":"main","bounds":{"x":0,"y":0,"width":1600,"height":900},"checkpoint":true}
          ]
        }
        """;

    [Fact]
    public void Complete_scene_deserializes_and_validates()
    {
        var scene = JsonSerializer.Deserialize<ScientificScene>(ValidSceneJson, ProtocolJson.Options);

        Assert.NotNull(scene);
        SceneGraphValidator.Validate(scene);
        Assert.IsType<SceneShapeNode>(scene.Nodes[0]);
        Assert.IsType<ScenePathNode>(scene.Nodes[1]);
    }

    [Fact]
    public void Unknown_node_kind_is_rejected()
    {
        var json = ValidSceneJson.Replace("\"kind\":\"shape\"", "\"kind\":\"unknown\"");

        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<ScientificScene>(json, ProtocolJson.Options));
    }

    [Fact]
    public void Duplicate_semantic_ids_are_rejected()
    {
        var json = ValidSceneJson.Replace("\"semantic_id\":\"wave\"", "\"semantic_id\":\"input\"");
        var scene = JsonSerializer.Deserialize<ScientificScene>(json, ProtocolJson.Options)!;

        var error = Assert.Throws<SceneContractException>(() => SceneGraphValidator.Validate(scene));

        Assert.Equal("DUPLICATE_SEMANTIC_ID", error.Code);
        Assert.Equal("$.nodes[1].semantic_id", error.FieldPath);
    }

    [Fact]
    public void Node_layer_must_be_declared()
    {
        var json = ValidSceneJson.Replace("\"layer\":\"content\"", "\"layer\":\"missing\"");
        var scene = JsonSerializer.Deserialize<ScientificScene>(json, ProtocolJson.Options)!;

        var error = Assert.Throws<SceneContractException>(() => SceneGraphValidator.Validate(scene));

        Assert.Equal("INVALID_SCENE", error.Code);
        Assert.Contains("layer", error.FieldPath);
    }

    [Fact]
    public void Local_path_coordinates_must_stay_inside_bounds()
    {
        var json = ValidSceneJson.Replace("\"x\":120,\"y\":30", "\"x\":121,\"y\":30");
        var scene = JsonSerializer.Deserialize<ScientificScene>(json, ProtocolJson.Options)!;

        var error = Assert.Throws<SceneContractException>(() => SceneGraphValidator.Validate(scene));

        Assert.Equal("$.nodes[1].commands[1]", error.FieldPath);
    }

    [Fact]
    public void At_least_one_region_checkpoint_is_required()
    {
        var json = ValidSceneJson.Replace("\"checkpoint\":true", "\"checkpoint\":false");
        var scene = JsonSerializer.Deserialize<ScientificScene>(json, ProtocolJson.Options)!;

        var error = Assert.Throws<SceneContractException>(() => SceneGraphValidator.Validate(scene));

        Assert.Equal("$.regions", error.FieldPath);
    }
}
