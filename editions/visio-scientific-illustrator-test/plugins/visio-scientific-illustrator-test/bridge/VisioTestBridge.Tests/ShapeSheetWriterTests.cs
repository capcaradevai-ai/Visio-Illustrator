using VisioTestBridge.Contracts;
using VisioTestBridge.Core;
using VisioTestBridge.Core.Styling;

namespace VisioTestBridge.Tests;

public sealed class ShapeSheetWriterTests
{
    [Fact]
    public void Complete_style_compiles_to_universal_ShapeSheet_formulas()
    {
        var style = new ShapeStyle(
            Fill: new FillStyle(
                "gradient",
                Color: "#112233",
                SecondaryColor: "#DDEEFF",
                TransparencyPercent: 12.5,
                SecondaryTransparencyPercent: 25,
                GradientAngleDeg: 90,
                GradientDirection: 0),
            Line: new LineStyle(
                Color: "#445566",
                WidthPt: 1.5,
                TransparencyPercent: 10,
                Pattern: 2,
                BeginArrow: "none",
                EndArrow: "triangle",
                EndArrowSize: "medium",
                Cap: "round"),
            Shadow: new ShadowStyle(true, "#778899", 15, 2, 3),
            Corner: new CornerStyle(8),
            RotationDeg: 12,
            OpacityPercent: 85);

        var cells = StyleFormulaCompiler.Compile(style);

        Assert.Equal("RGB(17,34,51)", cells["FillForegnd"]);
        Assert.Equal("RGB(221,238,255)", cells["FillBkgnd"]);
        Assert.Equal("12.5%", cells["FillForegndTrans"]);
        Assert.Equal("25%", cells["FillBkgndTrans"]);
        Assert.Equal("90 deg", cells["FillGradientAngle"]);
        Assert.Equal("RGB(68,85,102)", cells["LineColor"]);
        Assert.Equal("1.5 pt", cells["LineWeight"]);
        Assert.Equal("5", cells["EndArrow"]);
        Assert.Equal("3", cells["EndArrowSize"]);
        Assert.Equal("1", cells["LineCap"]);
        Assert.Equal("8 pt", cells["Rounding"]);
        Assert.Equal("12 deg", cells["Angle"]);
    }

    [Fact]
    public void Legacy_and_rich_style_fields_cannot_be_mixed()
    {
        var style = new ShapeStyle(
            FillColor: "#112233",
            Fill: new FillStyle("solid", Color: "#445566"));

        var error = Assert.Throws<BridgeCommandException>(() => StyleFormulaCompiler.Compile(style));

        Assert.Equal(BridgeErrorCodes.InvalidStyle, error.Code);
    }

    [Theory]
    [InlineData("none", "0")]
    [InlineData("solid", "1")]
    [InlineData("gradient", "1")]
    [InlineData("pattern", "5")]
    public void Fill_kinds_compile_without_silent_substitution(string kind, string expectedPattern)
    {
        var cells = StyleFormulaCompiler.Compile(new ShapeStyle(
            Fill: new FillStyle(
                kind,
                Color: kind == "none" ? null : "#112233",
                SecondaryColor: kind == "gradient" ? "#DDEEFF" : null,
                Pattern: kind == "pattern" ? 5 : null)));

        Assert.Equal(expectedPattern, cells["FillPattern"]);
    }

    [Fact]
    public void Gradient_requires_both_colors()
    {
        var style = new ShapeStyle(Fill: new FillStyle("gradient", Color: "#112233"));

        var error = Assert.Throws<BridgeCommandException>(() => StyleFormulaCompiler.Compile(style));

        Assert.Equal(BridgeErrorCodes.InvalidStyle, error.Code);
        Assert.Contains("secondary_color", error.Message);
    }

    [Fact]
    public void Formula_values_use_invariant_units()
    {
        Assert.Equal("1.25 pt", ShapeSheetValue.Points(1.25));
        Assert.Equal("45 deg", ShapeSheetValue.Degrees(45));
        Assert.Equal("12.5%", ShapeSheetValue.Percent(12.5));
        Assert.Equal("RGB(17,34,51)", ShapeSheetValue.Color("#112233"));
    }

    [Theory]
    [InlineData("Rounding", "length", 8, "pt", "8 pt")]
    [InlineData("Angle", "angle", 45, "deg", "45 deg")]
    [InlineData("FillForegndTrans", "percent", 12.5, "%", "12.5%")]
    public void Controlled_cells_compile_typed_values(
        string cell,
        string kind,
        double number,
        string unit,
        string expected)
    {
        var formula = ControlledShapeSheet.Compile(cell,
            new ShapeSheetValueContract(kind, Number: number, Unit: unit));

        Assert.Equal(expected, formula);
    }

    [Fact]
    public void Controlled_cells_reject_events_unknown_cells_and_untyped_lengths()
    {
        Assert.Throws<BridgeCommandException>(() => ControlledShapeSheet.Compile(
            "EventDblClick", new ShapeSheetValueContract("boolean", Boolean: true)));
        Assert.Throws<BridgeCommandException>(() => ControlledShapeSheet.Compile(
            "Rounding", new ShapeSheetValueContract("length", Number: 8)));
        Assert.Throws<BridgeCommandException>(() => ControlledShapeSheet.Compile(
            "Unknown", new ShapeSheetValueContract("integer", Integer: 1)));
    }
}
