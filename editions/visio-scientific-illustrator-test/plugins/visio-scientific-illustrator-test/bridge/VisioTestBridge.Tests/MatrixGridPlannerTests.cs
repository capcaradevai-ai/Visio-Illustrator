using System.Text.Json;
using VisioTestBridge.Contracts;
using VisioTestBridge.Core;
using VisioTestBridge.Core.Components;

namespace VisioTestBridge.Tests;

public sealed class MatrixGridPlannerTests
{
    [Fact]
    public void Matrix_cells_are_independent_editable_shapes_with_deterministic_heat_colors()
    {
        var contract = new MatrixComponentContract(
            Component("heatmap", "matrix"),
            [[0.0, 0.5], [0.75, 1.0]],
            Heatmap: true,
            new MatrixColorScale(0, 1, "#FFFFFF", "#FF0000"),
            RowLabels: ["r0", "r1"],
            ColumnLabels: ["c0", "c1"]);

        var first = MatrixComponentPlanner.CreatePlan(contract);
        var second = MatrixComponentPlanner.CreatePlan(contract);

        Assert.Contains(first.Primitives, item => item.SemanticId == "heatmap.cell.r1.c1");
        Assert.Equal("#FF0000", first.Primitives
            .Single(item => item.SemanticId == "heatmap.cell.r1.c1")
            .Parameters.GetProperty("fill").GetString());
        Assert.Equal(
            first.Primitives.Select(item => item.Parameters.GetRawText()),
            second.Primitives.Select(item => item.Parameters.GetRawText()));
    }

    [Fact]
    public void Grid_supports_labels_and_highlights_without_rasterization()
    {
        var contract = new GridComponentContract(
            Component("feature-grid", "grid"),
            2,
            3,
            [["a", "b", "c"], ["d", "e", "f"]],
            Highlights: [new GridCellAddress(1, 2)],
            RowLabels: ["low", "high"],
            ColumnLabels: ["x", "y", "z"]);

        var plan = GridComponentPlanner.CreatePlan(contract);

        Assert.Equal(6, plan.Primitives.Count(item =>
            item.ChildPath.StartsWith("cell.", StringComparison.Ordinal)));
        Assert.True(plan.Primitives
            .Single(item => item.SemanticId == "feature-grid.cell.r1.c2")
            .Parameters.GetProperty("highlighted").GetBoolean());
        Assert.DoesNotContain(plan.Primitives, item => item.Kind == "image");
    }

    [Theory]
    [InlineData(51, 1)]
    [InlineData(1, 51)]
    [InlineData(50, 51)]
    public void Rejects_matrix_or_grid_over_2500_cells(int rows, int columns)
    {
        var grid = new GridComponentContract(
            Component("grid", "grid"),
            rows,
            columns,
            null);

        var error = Assert.Throws<BridgeCommandException>(() =>
            GridComponentPlanner.CreatePlan(grid));

        Assert.Equal(BridgeErrorCodes.ComponentContractInvalid, error.Code);
    }

    private static CompositeComponentContract Component(string id, string type) =>
        new(
            id,
            type,
            "main",
            new PixelBox(10, 20, 240, 160),
            JsonSerializer.SerializeToElement(new
            {
                fill = "#FFFFFF",
                highlight_fill = "#FFE699",
            }),
            0);
}
