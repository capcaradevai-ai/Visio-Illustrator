using System.Globalization;
using System.Runtime.InteropServices;
using VisioBridge.Contracts;
using VisioBridge.Core.Styling;
using Visio = Microsoft.Office.Interop.Visio;

namespace VisioBridge.Core.Shapes;

public static class StyleMapper
{
    public static string ToRgbFormula(string input)
    {
        return ShapeSheetValue.Color(input);
    }

    internal static void Apply(Visio.Shape shape, ShapeStyle? style)
    {
        ShapeSheetWriter.Apply(shape, StyleFormulaCompiler.Compile(style));
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
