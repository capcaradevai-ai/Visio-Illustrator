using VisioTestBridge.Contracts;
using VisioTestBridge.Core;
using VisioTestBridge.Core.Quality;

namespace VisioTestBridge.Tests;

public sealed class SafePatchTests
{
    [Theory]
    [InlineData("expand_text_box")]
    [InlineData("move_inside_canvas")]
    public void Allowlisted_operations_are_accepted(string operation)
    {
        var patch = new AuditSafePatch(
            operation,
            ["shape"],
            3,
            new Dictionary<string, string>());

        Assert.Same(patch, SafePatchValidator.Validate(patch));
    }

    [Theory]
    [InlineData("change_text")]
    [InlineData("align_shapes")]
    [InlineData("distribute_shapes")]
    [InlineData("match_size")]
    [InlineData("set_z_order")]
    [InlineData("move_connector_waypoint")]
    [InlineData("resize_table_row")]
    [InlineData("resize_table_column")]
    [InlineData("reverse_connector")]
    [InlineData("replace_image")]
    [InlineData("rebuild_region")]
    [InlineData("move_passed_region")]
    public void Semantic_or_region_rebuild_operations_are_rejected(string operation)
    {
        var patch = new AuditSafePatch(
            operation,
            ["shape"],
            3,
            new Dictionary<string, string>());

        var error = Assert.Throws<BridgeCommandException>(
            () => SafePatchValidator.Validate(patch));

        Assert.Equal(BridgeErrorCodes.ComponentContractInvalid, error.Code);
    }
}
