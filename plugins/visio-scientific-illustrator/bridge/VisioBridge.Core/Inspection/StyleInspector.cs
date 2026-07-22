using System.Runtime.InteropServices;
using Visio = Microsoft.Office.Interop.Visio;

namespace VisioBridge.Core.Inspection;

internal static class StyleInspector
{
    private static readonly string[] Cells =
    [
        "FillPattern", "FillForegnd", "FillBkgnd", "FillForegndTrans", "FillBkgndTrans",
        "FillGradientDir", "FillGradientAngle", "LinePattern", "LineColor", "LineWeight",
        "LineColorTrans", "LineCap", "BeginArrow", "EndArrow", "BeginArrowSize", "EndArrowSize",
        "Rounding", "ShdwPattern", "ShdwForegnd", "ShdwOffsetX", "ShdwOffsetY",
        "Char.Font", "Char.Size", "Char.Color", "Char.Style", "Char.Position",
        "Para.HorzAlign", "VerticalAlign", "LeftMargin", "RightMargin", "TopMargin", "BottomMargin",
    ];

    public static IReadOnlyDictionary<string, string> Read(Visio.Shape shape)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var cellName in Cells)
        {
            if (shape.CellExistsU[cellName, (short)Visio.VisExistsFlags.visExistsAnywhere] == 0) continue;
            var cell = shape.CellsU[cellName];
            try { values[cellName] = cell.FormulaU; }
            finally { Marshal.FinalReleaseComObject(cell); }
        }
        return values;
    }

    public static IReadOnlyDictionary<string, bool> ReadProtection(Visio.Shape shape)
    {
        var names = new[]
        {
            "LockWidth", "LockHeight", "LockAspect", "LockMoveX", "LockMoveY", "LockRotate",
            "LockTextEdit", "LockDelete", "LockFormat", "LockGroup", "LockSelect", "LockVtxEdit",
            "LockBegin", "LockEnd",
        };
        var values = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var name in names)
        {
            if (shape.CellExistsU[name, (short)Visio.VisExistsFlags.visExistsAnywhere] == 0) continue;
            var cell = shape.CellsU[name];
            try { values[name] = Math.Abs(cell.ResultIU) > double.Epsilon; }
            finally { Marshal.FinalReleaseComObject(cell); }
        }
        return values;
    }
}
