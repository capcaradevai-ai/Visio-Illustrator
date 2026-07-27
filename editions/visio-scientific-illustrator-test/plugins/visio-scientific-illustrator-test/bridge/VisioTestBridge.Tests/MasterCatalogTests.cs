using VisioTestBridge.Core;
using VisioTestBridge.Core.Shapes;

namespace VisioTestBridge.Tests;

public sealed class MasterCatalogTests
{
    [Theory]
    [InlineData("rounded_rectangle", "Codex Rounded Rectangle")]
    [InlineData("diamond", "Codex Diamond")]
    [InlineData("cylinder", "Codex Cylinder")]
    [InlineData("hexagon", "Codex Hexagon")]
    [InlineData("triangle", "Codex Triangle")]
    [InlineData("parallelogram", "Codex Parallelogram")]
    [InlineData("cloud", "Codex Cloud")]
    [InlineData("tensor_face", "Codex Tensor Face")]
    [InlineData("video_frame", "Codex Video Frame")]
    [InlineData("latent_vector", "Codex Latent Vector")]
    [InlineData("snowflake", "Codex Snowflake")]
    [InlineData("router", "Codex Router")]
    [InlineData("antenna", "Codex Antenna")]
    [InlineData("stage_panel", "Codex Stage Panel")]
    [InlineData("container", "Codex Container")]
    public void Shape_type_maps_to_fixed_master_name(string shapeType, string masterName)
    {
        Assert.Equal(masterName, MasterCatalog.GetMasterName(shapeType));
    }

    [Fact]
    public void Unknown_master_type_is_not_substituted()
    {
        var error = Assert.Throws<BridgeCommandException>(
            () => MasterCatalog.GetMasterName("star"));

        Assert.Equal(BridgeErrorCodes.MasterNotFound, error.Code);
    }
}
