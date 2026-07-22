using System.Globalization;
using System.Runtime.InteropServices;
using VisioBridge.Contracts;
using Visio = Microsoft.Office.Interop.Visio;

namespace VisioBridge.Core.Styling;

public static class ShapeSheetValue
{
    public static string Points(double value) => Unit(value, "pt");
    public static string Degrees(double value) => Unit(value, "deg");
    public static string Percent(double value) => $"{Number(value)}%";

    public static string Color(string input)
    {
        if (input is null
            || input.Length != 7
            || input[0] != '#'
            || !int.TryParse(input.AsSpan(1, 2), NumberStyles.HexNumber, null, out var red)
            || !int.TryParse(input.AsSpan(3, 2), NumberStyles.HexNumber, null, out var green)
            || !int.TryParse(input.AsSpan(5, 2), NumberStyles.HexNumber, null, out var blue))
        {
            throw InvalidStyle($"颜色必须使用 #RRGGBB: {input}");
        }
        return $"RGB({red},{green},{blue})";
    }

    public static string Number(double value)
    {
        if (!double.IsFinite(value)) throw InvalidStyle("数值必须有限。");
        return value.ToString("0.######", CultureInfo.InvariantCulture);
    }

    public static void Range(double value, double minimum, double maximum, string name)
    {
        if (!double.IsFinite(value) || value < minimum || value > maximum)
            throw InvalidStyle($"{name} 必须在 {minimum}–{maximum} 范围内。");
    }

    internal static BridgeCommandException InvalidStyle(string message) =>
        new(BridgeErrorCodes.InvalidStyle, message);

    private static string Unit(double value, string unit) => $"{Number(value)} {unit}";
}

internal static class ShapeSheetWriter
{
    public static void Apply(Visio.Shape shape, IReadOnlyDictionary<string, string> formulas)
    {
        foreach (var (cellName, formula) in formulas)
        {
            if (shape.CellExistsU[cellName, (short)Visio.VisExistsFlags.visExistsAnywhere] == 0)
            {
                throw new BridgeCommandException(
                    BridgeErrorCodes.CellNotAvailable,
                    $"Shape 缺少样式单元格: {cellName}");
            }
            var cell = shape.CellsU[cellName];
            try
            {
                cell.FormulaU = formula;
            }
            finally
            {
                Marshal.FinalReleaseComObject(cell);
            }
        }
    }
}

internal static class StyleFormulaCompiler
{
    public static IReadOnlyDictionary<string, string> Compile(ShapeStyle? style)
    {
        var cells = new Dictionary<string, string>(StringComparer.Ordinal);
        if (style is null) return cells;

        var hasLegacy = style.FillColor is not null
            || style.FillTransparency is not null
            || style.StrokeColor is not null
            || style.StrokeWidthPt is not null
            || style.FontColor is not null
            || style.FontSizePt is not null
            || style.Dashed is not null;
        var hasRich = style.Fill is not null
            || style.Line is not null
            || style.TextBlock is not null
            || style.Shadow is not null
            || style.Corner is not null
            || style.OpacityPercent is not null;
        if (hasLegacy && hasRich)
            throw ShapeSheetValue.InvalidStyle("旧样式字段与新组合样式字段不能同时使用。");

        if (hasLegacy) CompileLegacy(style, cells);
        else CompileRich(style, cells);
        if (style.RotationDeg is not null)
        {
            ShapeSheetValue.Range(style.RotationDeg.Value, -360, 360, "rotation_deg");
            cells["Angle"] = ShapeSheetValue.Degrees(style.RotationDeg.Value);
        }
        return cells;
    }

    private static void CompileLegacy(ShapeStyle style, IDictionary<string, string> cells)
    {
        if (style.FillColor is not null) cells["FillForegnd"] = ShapeSheetValue.Color(style.FillColor);
        if (style.FillTransparency is not null)
        {
            ShapeSheetValue.Range(style.FillTransparency.Value, 0, 100, "fill_transparency");
            cells["FillForegndTrans"] = ShapeSheetValue.Percent(style.FillTransparency.Value);
        }
        if (style.StrokeColor is not null) cells["LineColor"] = ShapeSheetValue.Color(style.StrokeColor);
        if (style.StrokeWidthPt is not null)
        {
            ShapeSheetValue.Range(style.StrokeWidthPt.Value, 0, 50, "stroke_width_pt");
            cells["LineWeight"] = ShapeSheetValue.Points(style.StrokeWidthPt.Value);
        }
        if (style.FontColor is not null) cells["Char.Color"] = ShapeSheetValue.Color(style.FontColor);
        if (style.FontSizePt is not null)
        {
            ShapeSheetValue.Range(style.FontSizePt.Value, 1, 200, "font_size_pt");
            cells["Char.Size"] = ShapeSheetValue.Points(style.FontSizePt.Value);
        }
        if (style.Dashed is not null) cells["LinePattern"] = style.Dashed.Value ? "2" : "1";
    }

    private static void CompileRich(ShapeStyle style, IDictionary<string, string> cells)
    {
        FillStyleMapper.Compile(style.Fill, cells);
        LineStyleMapper.Compile(style.Line, cells);
        CompileTextBlock(style.TextBlock, cells);
        CompileShadow(style.Shadow, cells);
        if (style.Corner is not null)
        {
            ShapeSheetValue.Range(style.Corner.RadiusPt, 0, 1000, "corner.radius_pt");
            cells["Rounding"] = ShapeSheetValue.Points(style.Corner.RadiusPt);
        }
        if (style.OpacityPercent is not null)
        {
            ShapeSheetValue.Range(style.OpacityPercent.Value, 0, 100, "opacity_percent");
            var transparency = ShapeSheetValue.Percent(100 - style.OpacityPercent.Value);
            cells.TryAdd("FillForegndTrans", transparency);
            cells.TryAdd("FillBkgndTrans", transparency);
            cells.TryAdd("LineColorTrans", transparency);
        }
    }

    private static void CompileTextBlock(TextBlockStyle? style, IDictionary<string, string> cells)
    {
        if (style is null) return;
        if (style.HorizontalAlign is not null) cells["Para.HorzAlign"] = style.HorizontalAlign switch
        {
            "left" => "0", "center" => "1", "right" => "2", "justify" => "3",
            _ => throw ShapeSheetValue.InvalidStyle("horizontal_align 无效。"),
        };
        if (style.VerticalAlign is not null) cells["VerticalAlign"] = style.VerticalAlign switch
        {
            "top" => "0", "middle" => "1", "bottom" => "2",
            _ => throw ShapeSheetValue.InvalidStyle("vertical_align 无效。"),
        };
        AddMargin(cells, "LeftMargin", style.LeftMarginPt, "left_margin_pt");
        AddMargin(cells, "RightMargin", style.RightMarginPt, "right_margin_pt");
        AddMargin(cells, "TopMargin", style.TopMarginPt, "top_margin_pt");
        AddMargin(cells, "BottomMargin", style.BottomMarginPt, "bottom_margin_pt");
    }

    private static void CompileShadow(ShadowStyle? style, IDictionary<string, string> cells)
    {
        if (style is null) return;
        cells["ShdwPattern"] = style.Enabled ? "1" : "0";
        if (!style.Enabled) return;
        if (style.Color is not null) cells["ShdwForegnd"] = ShapeSheetValue.Color(style.Color);
        if (style.TransparencyPercent is not null)
        {
            ShapeSheetValue.Range(style.TransparencyPercent.Value, 0, 100, "shadow.transparency_percent");
            cells["ShdwForegndTrans"] = ShapeSheetValue.Percent(style.TransparencyPercent.Value);
        }
        AddMargin(cells, "ShdwOffsetX", style.OffsetXPt, "shadow.offset_x_pt", allowNegative: true);
        AddMargin(cells, "ShdwOffsetY", style.OffsetYPt, "shadow.offset_y_pt", allowNegative: true);
    }

    private static void AddMargin(
        IDictionary<string, string> cells,
        string cell,
        double? value,
        string name,
        bool allowNegative = false)
    {
        if (value is null) return;
        ShapeSheetValue.Range(value.Value, allowNegative ? -1000 : 0, 1000, name);
        cells[cell] = ShapeSheetValue.Points(value.Value);
    }
}
