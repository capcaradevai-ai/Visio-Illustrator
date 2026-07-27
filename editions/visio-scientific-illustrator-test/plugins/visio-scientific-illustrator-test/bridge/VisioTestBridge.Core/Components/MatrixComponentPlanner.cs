using System.Globalization;
using System.Text.Json;
using VisioTestBridge.Contracts;

namespace VisioTestBridge.Core.Components;

internal static class MatrixComponentPlanner
{
    public static CompositeComponentPlan CreatePlan(MatrixComponentContract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(contract.Component);
        ArgumentNullException.ThrowIfNull(contract.Values);
        CompositeComponentPlan.ValidateContract(contract.Component);
        if (!string.Equals(contract.Component.ComponentType, "matrix", StringComparison.Ordinal))
            throw Invalid("矩阵 component_type 必须是 matrix。");
        var rows = contract.Values.Count;
        var columns = rows == 0 ? 0 : contract.Values[0].Count;
        if (rows is < 1 or > 50 || columns is < 1 or > 50 || rows * columns > 2500
            || contract.Values.Any(row => row.Count != columns
                || row.Any(value => !double.IsFinite(value))))
            throw Invalid("矩阵必须是最多 50×50 的有限矩形数值数组。");
        ValidateLabels(contract.RowLabels, rows, "row_labels");
        ValidateLabels(contract.ColumnLabels, columns, "column_labels");
        if (contract.Heatmap && contract.ColorScale is null)
            throw Invalid("热力矩阵必须显式提供 color_scale。");
        if (contract.ColorScale is not null)
            ValidateScale(contract.ColorScale);

        var left = contract.RowLabels is null ? 0 : 44;
        var top = contract.ColumnLabels is null ? 0 : 22;
        var width = (contract.Component.Bounds.Width - left) / columns;
        var height = (contract.Component.Bounds.Height - top) / rows;
        if (width <= 0 || height <= 0)
            throw Invalid("矩阵 bounds 无法容纳标签和单元格。");
        var definitions = new List<CompositePrimitiveDefinition>();
        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                var value = contract.Values[row][column];
                definitions.Add(Cell(
                    $"cell.r{row}.c{column}",
                    new PixelBox(
                        contract.Component.Bounds.X + left + column * width,
                        contract.Component.Bounds.Y + top + row * height,
                        width,
                        height),
                    value.ToString("0.###", CultureInfo.InvariantCulture),
                    contract.Heatmap
                        ? Interpolate(contract.ColorScale!, value)
                        : "#FFFFFF",
                    highlighted: false));
            }
        }
        AddLabels(
            definitions,
            contract.Component.Bounds,
            contract.RowLabels,
            contract.ColumnLabels,
            left,
            top,
            width,
            height);
        return CompositeComponentPlan.Create(contract.Component, definitions);
    }

    internal static void AddLabels(
        ICollection<CompositePrimitiveDefinition> definitions,
        PixelBox bounds,
        IReadOnlyList<string>? rows,
        IReadOnlyList<string>? columns,
        double left,
        double top,
        double cellWidth,
        double cellHeight)
    {
        if (rows is not null)
        {
            for (var row = 0; row < rows.Count; row++)
                definitions.Add(Cell(
                    $"row_label.{row}",
                    new PixelBox(bounds.X, bounds.Y + top + row * cellHeight, left, cellHeight),
                    rows[row],
                    "#FFFFFF",
                    false));
        }
        if (columns is not null)
        {
            for (var column = 0; column < columns.Count; column++)
                definitions.Add(Cell(
                    $"column_label.{column}",
                    new PixelBox(bounds.X + left + column * cellWidth, bounds.Y, cellWidth, top),
                    columns[column],
                    "#FFFFFF",
                    false));
        }
    }

    internal static CompositePrimitiveDefinition Cell(
        string path,
        PixelBox bounds,
        string text,
        string fill,
        bool highlighted) =>
        new(
            path,
            "shape",
            bounds,
            JsonSerializer.SerializeToElement(new
            {
                shape = "rectangle",
                text,
                fill,
                highlighted,
                editable = true,
            }, ProtocolJson.Options));

    private static string Interpolate(MatrixColorScale scale, double value)
    {
        var ratio = Math.Clamp(
            (value - scale.Minimum) / (scale.Maximum - scale.Minimum),
            0,
            1);
        var first = ParseColor(scale.MinimumColor);
        var second = ParseColor(scale.MaximumColor);
        var red = (int)Math.Round(first.Red + (second.Red - first.Red) * ratio);
        var green = (int)Math.Round(first.Green + (second.Green - first.Green) * ratio);
        var blue = (int)Math.Round(first.Blue + (second.Blue - first.Blue) * ratio);
        return $"#{red:X2}{green:X2}{blue:X2}";
    }

    private static void ValidateScale(MatrixColorScale scale)
    {
        if (!double.IsFinite(scale.Minimum)
            || !double.IsFinite(scale.Maximum)
            || scale.Minimum >= scale.Maximum)
            throw Invalid("color_scale 必须使用递增有限 domain。");
        _ = ParseColor(scale.MinimumColor);
        _ = ParseColor(scale.MaximumColor);
    }

    private static (int Red, int Green, int Blue) ParseColor(string value)
    {
        if (value is null || value.Length != 7 || value[0] != '#'
            || !int.TryParse(value.AsSpan(1, 2), NumberStyles.HexNumber, null, out var red)
            || !int.TryParse(value.AsSpan(3, 2), NumberStyles.HexNumber, null, out var green)
            || !int.TryParse(value.AsSpan(5, 2), NumberStyles.HexNumber, null, out var blue))
            throw Invalid("颜色必须使用 #RRGGBB。");
        return (red, green, blue);
    }

    internal static void ValidateLabels(
        IReadOnlyList<string>? labels,
        int expected,
        string field)
    {
        if (labels is not null
            && (labels.Count != expected || labels.Any(value => value is null)))
            throw Invalid($"{field} 数量必须匹配对应维度。");
    }

    private static BridgeCommandException Invalid(string message) =>
        new(BridgeErrorCodes.ComponentContractInvalid, message);
}
