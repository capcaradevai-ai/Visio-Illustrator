using VisioTestBridge.Contracts;
using VisioTestBridge.Core;
using VisioTestBridge.Core.Layout;

namespace VisioTestBridge.Tests;

public sealed class LayoutServiceTests
{
    private static readonly LayoutShapeState[] Shapes =
    [
        new("a", new PixelBox(10, 20, 100, 50), 1),
        new("b", new PixelBox(200, 100, 80, 100), 2),
        new("c", new PixelBox(400, 240, 120, 60), 3),
    ];

    [Theory]
    [InlineData("left", 10, 100)]
    [InlineData("center", 20, 100)]
    [InlineData("right", 30, 100)]
    [InlineData("top", 200, 20)]
    [InlineData("middle", 200, -5)]
    [InlineData("bottom", 200, -30)]
    public void Align_uses_reference_page_space_edges(
        string alignment,
        double expectedBX,
        double expectedBY)
    {
        var desired = LayoutPlanner.Align(Shapes, alignment, "a");

        Assert.Equal(expectedBX, desired["b"].X, 6);
        Assert.Equal(expectedBY, desired["b"].Y, 6);
    }

    [Fact]
    public void Horizontal_equal_gap_distribution_preserves_outer_bounds()
    {
        var desired = LayoutPlanner.Distribute(
            Shapes,
            "horizontal",
            "equal_gap",
            preserveOuterBounds: true);

        Assert.Equal(10, desired["a"].X, 6);
        Assert.Equal(215, desired["b"].X, 6);
        Assert.Equal(400, desired["c"].X, 6);
    }

    [Fact]
    public void Vertical_equal_center_distribution_preserves_first_and_last_centers()
    {
        var desired = LayoutPlanner.Distribute(
            Shapes,
            "vertical",
            "equal_center",
            preserveOuterBounds: true);

        Assert.Equal(20, desired["a"].Y, 6);
        Assert.Equal(107.5, desired["b"].Y, 6);
        Assert.Equal(240, desired["c"].Y, 6);
    }

    [Theory]
    [InlineData("width", 100, 100)]
    [InlineData("height", 80, 50)]
    [InlineData("both", 100, 50)]
    public void Match_size_uses_the_reference_shape(
        string dimension,
        double expectedWidth,
        double expectedHeight)
    {
        var desired = LayoutPlanner.MatchSize(Shapes, "a", dimension);

        Assert.Equal(expectedWidth, desired["b"].Width, 6);
        Assert.Equal(expectedHeight, desired["b"].Height, 6);
    }

    [Fact]
    public void Protection_fields_compile_only_to_the_explicit_Visio_cells()
    {
        var cells = LayoutPlanner.CompileProtection(new ProtectionFields(
            MoveX: true,
            MoveY: false,
            Width: true,
            Height: false,
            Rotate: true,
            TextEdit: true,
            Delete: false,
            Format: true,
            Select: false,
            Aspect: true,
            VertexEdit: true,
            Begin: true,
            End: true));

        Assert.Equal("1", cells["LockMoveX"]);
        Assert.Equal("0", cells["LockMoveY"]);
        Assert.Equal("1", cells["LockAspect"]);
        Assert.Equal("1", cells["LockVtxEdit"]);
        Assert.Equal("1", cells["LockBegin"]);
        Assert.Equal("1", cells["LockEnd"]);
        Assert.DoesNotContain("LockCrop", cells.Keys);
    }

    [Fact]
    public void Layout_contracts_reject_invalid_z_order_and_duplicate_ids()
    {
        Assert.Throws<BridgeCommandException>(() =>
            LayoutPlanner.ValidateZOrder(new SetZOrderCommand("a", "absolute")));
        Assert.Throws<BridgeCommandException>(() =>
            LayoutPlanner.ValidateSemanticIds(["a", "a"], minimum: 2));
    }
}
