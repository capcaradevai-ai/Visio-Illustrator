using System.Text.Json;
using VisioTestBridge.Contracts;
using VisioTestBridge.Core;
using VisioTestBridge.Core.Components;

namespace VisioTestBridge.Tests;

public sealed class TableComponentPlannerTests
{
    [Fact]
    public void Plans_independent_editable_cells_and_stable_outer_borders()
    {
        var contract = CreateContract(
            [
                [new TableCell("Method"), new TableCell("Accuracy")],
                [new TableCell("Ours"), new TableCell("94.2%", "#EAF2F8")],
            ],
            headerRow: true);

        var plan = TableComponentPlanner.CreatePlan(contract);

        Assert.Equal(
            [
                "results-table.cell.r0.c0",
                "results-table.cell.r0.c1",
                "results-table.cell.r1.c0",
                "results-table.cell.r1.c1",
                "results-table.border.top",
                "results-table.border.right",
                "results-table.border.bottom",
                "results-table.border.left",
            ],
            plan.Primitives.Select(item => item.SemanticId));
        Assert.All(
            plan.Primitives.Take(4),
            primitive =>
            {
                Assert.Equal("shape", primitive.Kind);
                Assert.True(primitive.Parameters.GetProperty("editable_text").GetBoolean());
                Assert.Equal("rectangle", primitive.Parameters.GetProperty("shape").GetString());
            });
        Assert.Equal(
            "Method",
            plan.Primitives[0].Parameters.GetProperty("text").GetString());
        Assert.True(
            plan.Primitives[0].Parameters.GetProperty("is_header").GetBoolean());
        Assert.Equal(
            "#EAF2F8",
            plan.Primitives[3].Parameters.GetProperty("fill").GetString());
    }

    [Fact]
    public void Uses_explicit_row_heights_and_column_widths_deterministically()
    {
        var contract = CreateContract(
            [
                [new TableCell("A"), new TableCell("B")],
                [new TableCell("C"), new TableCell("D")],
            ],
            rowHeights: [30, 70],
            columnWidths: [80, 120]);

        var first = TableComponentPlanner.CreatePlan(contract);
        var second = TableComponentPlanner.CreatePlan(contract);

        Assert.Equal(
            first.Primitives.Select(ComparablePrimitive),
            second.Primitives.Select(ComparablePrimitive));
        Assert.Equal(new PixelBox(10, 20, 80, 30), first.Primitives[0].Bounds);
        Assert.Equal(new PixelBox(90, 20, 120, 30), first.Primitives[1].Bounds);
        Assert.Equal(new PixelBox(10, 50, 80, 70), first.Primitives[2].Bounds);
    }

    private static object ComparablePrimitive(CompositePrimitivePlan primitive) => new
    {
        primitive.SemanticId,
        primitive.ParentSemanticId,
        primitive.ChildPath,
        primitive.Kind,
        primitive.Bounds,
        Parameters = primitive.Parameters.GetRawText(),
    };

    [Theory]
    [InlineData(0, 1)]
    [InlineData(51, 1)]
    [InlineData(1, 0)]
    [InlineData(1, 51)]
    public void Rejects_row_or_column_count_outside_one_to_fifty(
        int rows,
        int columns)
    {
        var cells = Enumerable.Range(0, rows)
            .Select(_ => Enumerable.Range(0, columns)
                .Select(_ => new TableCell(""))
                .ToArray() as IReadOnlyList<TableCell>)
            .ToArray();
        var contract = CreateContract(cells);

        var error = Assert.Throws<BridgeCommandException>(() =>
            TableComponentPlanner.CreatePlan(contract));

        Assert.Equal(BridgeErrorCodes.ComponentContractInvalid, error.Code);
    }

    [Fact]
    public void Rejects_ragged_rows_as_invalid_component_contract()
    {
        var contract = CreateContract(
            [
                [new TableCell("A"), new TableCell("B")],
                [new TableCell("C")],
            ]);

        var error = Assert.Throws<BridgeCommandException>(() =>
            TableComponentPlanner.CreatePlan(contract));

        Assert.Equal(BridgeErrorCodes.ComponentContractInvalid, error.Code);
    }

    [Fact]
    public void Reports_merged_cells_as_an_unavailable_capability()
    {
        var contract = CreateContract(
            [[new TableCell("A")]],
            mergedCells: [new TableMergedCell(0, 0, 1, 1)]);

        var error = Assert.Throws<BridgeCommandException>(() =>
            TableComponentPlanner.CreatePlan(contract));

        Assert.Equal(BridgeErrorCodes.CapabilityNotAvailable, error.Code);
    }

    private static TableComponentContract CreateContract(
        IReadOnlyList<IReadOnlyList<TableCell>> cells,
        bool headerRow = false,
        IReadOnlyList<double>? rowHeights = null,
        IReadOnlyList<double>? columnWidths = null,
        IReadOnlyList<TableMergedCell>? mergedCells = null) =>
        new(
            new CompositeComponentContract(
                "results-table",
                "table",
                "results",
                new PixelBox(10, 20, 200, 100),
                JsonSerializer.SerializeToElement(new
                {
                    border_color = "#222222",
                    header_fill = "#D9EAF7",
                }),
                3),
            cells,
            new TableLayout(
                HeaderRow: headerRow,
                HeaderColumn: false,
                RowHeights: rowHeights,
                ColumnWidths: columnWidths,
                HorizontalAlignment: "center",
                VerticalAlignment: "middle"),
            mergedCells);
}
