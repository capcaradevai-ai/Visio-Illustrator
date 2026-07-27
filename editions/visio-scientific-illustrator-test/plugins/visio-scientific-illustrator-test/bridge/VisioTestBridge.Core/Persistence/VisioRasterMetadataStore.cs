using System.Runtime.InteropServices;
using Visio = Microsoft.Office.Interop.Visio;

namespace VisioTestBridge.Core.Persistence;

internal sealed class VisioRasterMetadataStore(Visio.Shape shape) : IRasterMetadataStore
{
    public void WriteUserCell(string cellName, string value)
    {
        const string prefix = "User.";
        if (!cellName.StartsWith(prefix, StringComparison.Ordinal))
            throw new ArgumentException("User Cell 名称必须使用 User. 前缀。", nameof(cellName));
        EnsureRow(
            (short)Visio.VisSectionIndices.visSectionUser,
            cellName[prefix.Length..]);
        WriteCell(cellName, value);
    }

    public void WriteShapeData(string cellName, string label, string value)
    {
        const string prefix = "Prop.";
        if (!cellName.StartsWith(prefix, StringComparison.Ordinal))
            throw new ArgumentException("Shape Data 名称必须使用 Prop. 前缀。", nameof(cellName));
        var rowName = cellName[prefix.Length..];
        EnsureRow((short)Visio.VisSectionIndices.visSectionProp, rowName);
        WriteCell($"{cellName}.Label", label);
        WriteCell(cellName, value);
    }

    private void EnsureRow(short section, string rowName)
    {
        if (shape.SectionExists[
                section,
                (short)Visio.VisExistsFlags.visExistsAnywhere] == 0)
        {
            shape.AddSection(section);
        }
        var universalCell = section == (short)Visio.VisSectionIndices.visSectionUser
            ? $"User.{rowName}"
            : $"Prop.{rowName}";
        if (shape.CellExistsU[
                universalCell,
                (short)Visio.VisExistsFlags.visExistsAnywhere] == 0)
        {
            shape.AddNamedRow(
                section,
                rowName,
                (short)Visio.VisRowTags.visTagDefault);
        }
    }

    private void WriteCell(string cellName, string value)
    {
        var cell = shape.CellsU[cellName];
        try
        {
            cell.FormulaU = $"\"{value.Replace("\"", "\"\"")}\"";
        }
        finally
        {
            Marshal.FinalReleaseComObject(cell);
        }
    }
}
