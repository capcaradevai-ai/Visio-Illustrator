using VisioTestBridge.Contracts;

namespace VisioTestBridge.IntegrationTests;

public sealed class InspectionIntegrationTests
{
    [Trait("Category", "VisioIntegration")]
    [Fact]
    public void Controlled_cells_geometry_connection_points_and_rich_snapshot_are_live()
    {
        using var fixture = VisioFixture.StartVisible();
        fixture.AddPath(new AddPathCommand(
            "wave",
            new PixelBox(100, 100, 600, 220),
            [new PathSection([
                new ScenePathCommand("M", 0, 110),
                new ScenePathCommand("C", 600, 110, 120, 0, 450, 220),
            ])],
            new ShapeStyle(
                Fill: new FillStyle("none"),
                Line: new LineStyle("#245B91", 2))));

        var written = fixture.SetCells(new SetCellsCommand(
            "wave",
            [
                new ShapeSheetWrite("LineWeight", new ShapeSheetValueContract("length", Number: 2.5, Unit: "pt")),
                new ShapeSheetWrite("LockAspect", new ShapeSheetValueContract("boolean", Boolean: true)),
            ]));
        fixture.AddConnectionPoint(new AddConnectionPointCommand(
            "wave", "right_upper", 1, .25, 1, 0));
        var geometry = fixture.GetGeometry(new GetGeometryCommand("wave"));
        var snapshot = fixture.Inspect().Shapes.Single(shape => shape.SemanticId == "wave");

        Assert.Equal(2, written.Cells.Count);
        Assert.Contains("2.5 pt", written.Cells.Single(cell => cell.Cell == "LineWeight").FormulaU);
        Assert.NotEmpty(geometry.Sections);
        Assert.Contains(geometry.Sections.SelectMany(section => section.Rows), row => row.Cells.Count > 0);
        Assert.Equal("shape", snapshot.Type);
        Assert.True(snapshot.ZOrder > 0);
        Assert.True(snapshot.Geometry?.SectionCount >= 1);
        Assert.True(snapshot.Geometry?.RowCount >= 2);
        Assert.Equal(true, snapshot.Protection?["LockAspect"]);
        Assert.Contains("right_upper", snapshot.ConnectionPoints ?? []);
        Assert.Contains("LineWeight", snapshot.StyleCells?.Keys ?? []);
    }

    [Trait("Category", "VisioIntegration")]
    [Fact]
    public void Event_and_unknown_cells_are_rejected_before_COM_write()
    {
        using var fixture = VisioFixture.WithOneShape();
        var error = Assert.Throws<VisioTestBridge.Core.BridgeCommandException>(() =>
            fixture.SetCells(new SetCellsCommand(
                "preview-shape",
                [new ShapeSheetWrite(
                    "EventDblClick",
                    new ShapeSheetValueContract("boolean", Boolean: true))])));

        Assert.Equal(VisioTestBridge.Core.BridgeErrorCodes.InvalidArgument, error.Code);
    }
}
