using System.Runtime.InteropServices;
using VisioTestBridge.Contracts;
using Visio = Microsoft.Office.Interop.Visio;

namespace VisioTestBridge.Core.Inspection;

internal static class GeometryInspector
{
    private const short First = (short)Visio.VisSectionIndices.visSectionFirstComponent;

    public static GeometrySummary ReadSummary(Visio.Shape shape)
    {
        var sections = 0;
        var rows = 0;
        var closed = false;
        for (short section = First; section < First + 256; section++)
        {
            if (shape.SectionExists[section, (short)Visio.VisExistsFlags.visExistsAnywhere] == 0) continue;
            sections++;
            var count = shape.RowCount[section];
            rows += Math.Max(0, count - 1);
            for (short row = 1; row < count; row++)
            {
                if (shape.RowType[section, row] == (short)Visio.VisRowTags.visTagRelLineTo
                    && IsClosingRow(shape, section, row)) closed = true;
            }
        }
        return new GeometrySummary(sections, rows, closed);
    }

    public static IReadOnlyList<string> ReadConnectionPointNames(Visio.Shape shape)
    {
        var section = (short)Visio.VisSectionIndices.visSectionConnectionPts;
        if (shape.SectionExists[section, (short)Visio.VisExistsFlags.visExistsAnywhere] == 0) return [];
        var names = new List<string>();
        for (short row = 0; row < shape.RowCount[section]; row++)
        {
            var cell = shape.CellsSRC[section, row, (short)Visio.VisCellIndices.visCnnctX];
            try
            {
                var name = cell.RowNameU;
                if (!string.IsNullOrWhiteSpace(name)) names.Add(name);
            }
            finally { Marshal.FinalReleaseComObject(cell); }
        }
        return names;
    }

    private static bool IsClosingRow(Visio.Shape shape, short section, short row)
    {
        Visio.Cell? x = null;
        Visio.Cell? y = null;
        try
        {
            x = shape.CellsSRC[section, row, (short)Visio.VisCellIndices.visX];
            y = shape.CellsSRC[section, row, (short)Visio.VisCellIndices.visY];
            return x.FormulaU.Contains("Geometry", StringComparison.OrdinalIgnoreCase)
                || y.FormulaU.Contains("Geometry", StringComparison.OrdinalIgnoreCase);
        }
        catch (COMException) { return false; }
        finally
        {
            if (x is not null) Marshal.FinalReleaseComObject(x);
            if (y is not null) Marshal.FinalReleaseComObject(y);
        }
    }
}
