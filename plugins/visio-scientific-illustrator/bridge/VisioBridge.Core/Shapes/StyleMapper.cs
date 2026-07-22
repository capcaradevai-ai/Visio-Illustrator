using System.Globalization;
using System.Runtime.InteropServices;
using VisioBridge.Contracts;
using Visio = Microsoft.Office.Interop.Visio;

namespace VisioBridge.Core.Shapes;

public static class StyleMapper
{
    public static string ToRgbFormula(string input)
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

    internal static void Apply(Visio.Shape shape, ShapeStyle? style)
    {
        if (style is null) return;
        if (style.FillColor is not null) SetFormula(shape, "FillForegnd", ToRgbFormula(style.FillColor));
        if (style.FillTransparency is not null)
        {
            ValidateRange(style.FillTransparency.Value, 0, 100, "fill_transparency");
            SetFormula(shape, "FillForegndTrans", Percent(style.FillTransparency.Value));
        }
        if (style.StrokeColor is not null) SetFormula(shape, "LineColor", ToRgbFormula(style.StrokeColor));
        if (style.StrokeWidthPt is not null)
        {
            ValidateRange(style.StrokeWidthPt.Value, 0, 50, "stroke_width_pt");
            SetFormula(shape, "LineWeight", Points(style.StrokeWidthPt.Value));
        }
        if (style.FontColor is not null) SetFormula(shape, "Char.Color", ToRgbFormula(style.FontColor));
        if (style.FontSizePt is not null)
        {
            ValidateRange(style.FontSizePt.Value, 1, 200, "font_size_pt");
            SetFormula(shape, "Char.Size", Points(style.FontSizePt.Value));
        }
        if (style.RotationDeg is not null)
        {
            ValidateRange(style.RotationDeg.Value, -360, 360, "rotation_deg");
            SetFormula(shape, "Angle", Degrees(style.RotationDeg.Value));
        }
        if (style.Dashed is not null)
        {
            SetFormula(shape, "LinePattern", style.Dashed.Value ? "2" : "1");
        }
    }

    internal static void ApplyTextDefaults(Visio.Shape shape, bool transparent)
    {
        SetFormula(shape, "Para.HorzAlign", "1");
        SetFormula(shape, "VerticalAlign", "1");
        if (!transparent) return;
        SetFormula(shape, "FillForegndTrans", "100%");
        SetFormula(shape, "LinePattern", "0");
    }

    internal static void SetFormula(Visio.Shape shape, string cellName, string formula)
    {
        if (shape.CellExistsU[cellName, (short)Visio.VisExistsFlags.visExistsAnywhere] == 0)
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.CellNotFound,
                $"Shape 缺少必需单元格: {cellName}");
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

    internal static void SetResultInches(Visio.Shape shape, string cellName, double value)
    {
        if (!double.IsFinite(value)) throw InvalidStyle($"{cellName} 不是有限数值。");
        if (shape.CellExistsU[cellName, (short)Visio.VisExistsFlags.visExistsAnywhere] == 0)
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.CellNotFound,
                $"Shape 缺少必需单元格: {cellName}");
        }
        var cell = shape.CellsU[cellName];
        try
        {
            cell.ResultIU = value;
        }
        finally
        {
            Marshal.FinalReleaseComObject(cell);
        }
    }

    private static string Points(double value) =>
        $"{value.ToString("0.######", CultureInfo.InvariantCulture)} pt";

    private static string Degrees(double value) =>
        $"{value.ToString("0.######", CultureInfo.InvariantCulture)} deg";

    private static string Percent(double value) =>
        $"{value.ToString("0.######", CultureInfo.InvariantCulture)}%";

    private static void ValidateRange(double value, double minimum, double maximum, string name)
    {
        if (!double.IsFinite(value) || value < minimum || value > maximum)
        {
            throw InvalidStyle($"{name} 必须在 {minimum}–{maximum} 范围内。");
        }
    }

    private static BridgeCommandException InvalidStyle(string message) =>
        new(BridgeErrorCodes.InvalidStyle, message);
}
