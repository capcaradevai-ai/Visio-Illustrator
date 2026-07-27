using System.Text.Json;
using VisioTestBridge.Contracts;

namespace VisioTestBridge.Core.Components;

internal static class TableComponentPlanner
{
    private const int MaximumDimension = 50;

    public static CompositeComponentPlan CreatePlan(TableComponentContract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(contract.Component);
        ArgumentNullException.ThrowIfNull(contract.Cells);
        ArgumentNullException.ThrowIfNull(contract.Layout);
        CompositeComponentPlan.ValidateContract(contract.Component);
        if (!string.Equals(contract.Component.ComponentType, "table", StringComparison.Ordinal))
            throw Invalid("表格合同的 component_type 必须为 table。");
        if (contract.MergedCells is { Count: > 0 })
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.CapabilityNotAvailable,
                "0.4.0-test.1 不支持合并单元格。");
        }

        var rowCount = contract.Cells.Count;
        if (rowCount is < 1 or > MaximumDimension)
            throw Invalid("表格行数必须在 1 到 50 之间。");
        var columnCount = contract.Cells[0]?.Count ?? 0;
        if (columnCount is < 1 or > MaximumDimension)
            throw Invalid("表格列数必须在 1 到 50 之间。");
        if (contract.Cells.Any(row => row is null || row.Count != columnCount))
            throw Invalid("表格每一行必须具有相同的列数。");

        ValidateAlignment(
            contract.Layout.HorizontalAlignment,
            ["left", "center", "right"],
            "horizontal_alignment");
        ValidateAlignment(
            contract.Layout.VerticalAlignment,
            ["top", "middle", "bottom"],
            "vertical_alignment");

        var bounds = contract.Component.Bounds;
        var heights = ResolveDimensions(
            contract.Layout.RowHeights,
            rowCount,
            bounds.Height,
            "row_heights");
        var widths = ResolveDimensions(
            contract.Layout.ColumnWidths,
            columnCount,
            bounds.Width,
            "column_widths");
        var borderColor = ReadStyle(contract.Component.Style, "border_color", "#222222");
        var headerFill = ReadStyle(contract.Component.Style, "header_fill", "#E8EEF5");
        var definitions = new List<CompositePrimitiveDefinition>(
            checked(rowCount * columnCount + 4));

        var y = bounds.Y;
        for (var row = 0; row < rowCount; row++)
        {
            var x = bounds.X;
            for (var column = 0; column < columnCount; column++)
            {
                var cell = contract.Cells[row][column]
                    ?? throw Invalid($"单元格 r{row}c{column} 不能为空。");
                ValidateCell(cell, row, column);
                var isHeader = contract.Layout.HeaderRow && row == 0
                    || contract.Layout.HeaderColumn && column == 0;
                var fill = cell.Fill ?? (isHeader ? headerFill : "#FFFFFF");
                definitions.Add(new CompositePrimitiveDefinition(
                    $"cell.r{row}.c{column}",
                    "shape",
                    new PixelBox(x, y, widths[column], heights[row]),
                    JsonSerializer.SerializeToElement(new
                    {
                        shape = "rectangle",
                        text = cell.Text,
                        editable_text = true,
                        fill,
                        text_color = cell.TextColor ?? "#111111",
                        font_size = cell.FontSize ?? 10,
                        bold = cell.Bold || isHeader,
                        is_header = isHeader,
                        horizontal_alignment = contract.Layout.HorizontalAlignment,
                        vertical_alignment = contract.Layout.VerticalAlignment,
                        border_color = borderColor,
                    }, ProtocolJson.Options)));
                x += widths[column];
            }
            y += heights[row];
        }

        AddBorderDefinitions(definitions, bounds, borderColor);
        return CompositeComponentPlan.Create(contract.Component, definitions);
    }

    private static void AddBorderDefinitions(
        ICollection<CompositePrimitiveDefinition> definitions,
        PixelBox bounds,
        string color)
    {
        const double thickness = 1;
        definitions.Add(Border(
            "border.top",
            new PixelBox(bounds.X, bounds.Y, bounds.Width, thickness),
            color));
        definitions.Add(Border(
            "border.right",
            new PixelBox(
                bounds.X + bounds.Width - thickness,
                bounds.Y,
                thickness,
                bounds.Height),
            color));
        definitions.Add(Border(
            "border.bottom",
            new PixelBox(
                bounds.X,
                bounds.Y + bounds.Height - thickness,
                bounds.Width,
                thickness),
            color));
        definitions.Add(Border(
            "border.left",
            new PixelBox(bounds.X, bounds.Y, thickness, bounds.Height),
            color));
    }

    private static CompositePrimitiveDefinition Border(
        string childPath,
        PixelBox bounds,
        string color) =>
        new(
            childPath,
            "path",
            bounds,
            JsonSerializer.SerializeToElement(new
            {
                stroke = color,
                stroke_width = 1,
                editable = true,
            }, ProtocolJson.Options));

    private static double[] ResolveDimensions(
        IReadOnlyList<double>? supplied,
        int count,
        double total,
        string field)
    {
        if (supplied is null)
            return Enumerable.Repeat(total / count, count).ToArray();
        if (supplied.Count != count
            || supplied.Any(value => !double.IsFinite(value) || value <= 0))
        {
            throw Invalid($"{field} 必须包含与对应维度相同数量的有限正数。");
        }
        if (Math.Abs(supplied.Sum() - total) > 0.001)
            throw Invalid($"{field} 的总和必须等于组件边界的对应尺寸。");
        return supplied.ToArray();
    }

    private static void ValidateCell(TableCell cell, int row, int column)
    {
        if (cell.Text is null)
            throw Invalid($"单元格 r{row}c{column} 的文本不能为空引用。");
        if (cell.FontSize is not null
            && (!double.IsFinite(cell.FontSize.Value) || cell.FontSize.Value <= 0))
        {
            throw Invalid($"单元格 r{row}c{column} 的字号必须是有限正数。");
        }
    }

    private static void ValidateAlignment(
        string value,
        IReadOnlyCollection<string> allowed,
        string field)
    {
        if (!allowed.Contains(value, StringComparer.Ordinal))
            throw Invalid($"{field} 使用了不支持的值。");
    }

    private static string ReadStyle(
        JsonElement? style,
        string property,
        string defaultValue)
    {
        if (style is null || style.Value.ValueKind is JsonValueKind.Null)
            return defaultValue;
        return style.Value.TryGetProperty(property, out var value)
            && value.ValueKind == JsonValueKind.String
            ? value.GetString()!
            : defaultValue;
    }

    private static BridgeCommandException Invalid(string message) =>
        new(BridgeErrorCodes.ComponentContractInvalid, message);
}
