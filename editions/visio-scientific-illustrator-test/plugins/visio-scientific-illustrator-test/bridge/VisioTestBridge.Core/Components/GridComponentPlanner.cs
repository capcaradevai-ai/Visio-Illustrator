using VisioTestBridge.Contracts;

namespace VisioTestBridge.Core.Components;

internal static class GridComponentPlanner
{
    public static CompositeComponentPlan CreatePlan(GridComponentContract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(contract.Component);
        CompositeComponentPlan.ValidateContract(contract.Component);
        if (!string.Equals(contract.Component.ComponentType, "grid", StringComparison.Ordinal)
            || contract.Rows is < 1 or > 50
            || contract.Columns is < 1 or > 50
            || contract.Rows * contract.Columns > 2500)
            throw Invalid("网格必须是最多 50×50 且不超过 2500 cells。");
        if (contract.Labels is not null
            && (contract.Labels.Count != contract.Rows
                || contract.Labels.Any(row => row.Count != contract.Columns)))
            throw Invalid("网格 labels 必须匹配行列维度。");
        MatrixComponentPlanner.ValidateLabels(contract.RowLabels, contract.Rows, "row_labels");
        MatrixComponentPlanner.ValidateLabels(contract.ColumnLabels, contract.Columns, "column_labels");
        var highlights = (contract.Highlights ?? [])
            .Select(address =>
            {
                if (address.Row < 0 || address.Row >= contract.Rows
                    || address.Column < 0 || address.Column >= contract.Columns)
                    throw Invalid("highlight cell 超出网格。");
                return (address.Row, address.Column);
            })
            .ToHashSet();
        var left = contract.RowLabels is null ? 0 : 44;
        var top = contract.ColumnLabels is null ? 0 : 22;
        var width = (contract.Component.Bounds.Width - left) / contract.Columns;
        var height = (contract.Component.Bounds.Height - top) / contract.Rows;
        if (width <= 0 || height <= 0)
            throw Invalid("网格 bounds 无法容纳标签和单元格。");
        var definitions = new List<CompositePrimitiveDefinition>();
        for (var row = 0; row < contract.Rows; row++)
        {
            for (var column = 0; column < contract.Columns; column++)
            {
                var highlighted = highlights.Contains((row, column));
                definitions.Add(MatrixComponentPlanner.Cell(
                    $"cell.r{row}.c{column}",
                    new PixelBox(
                        contract.Component.Bounds.X + left + column * width,
                        contract.Component.Bounds.Y + top + row * height,
                        width,
                        height),
                    contract.Labels?[row][column] ?? string.Empty,
                    highlighted ? "#FFE699" : "#FFFFFF",
                    highlighted));
            }
        }
        MatrixComponentPlanner.AddLabels(
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

    private static BridgeCommandException Invalid(string message) =>
        new(BridgeErrorCodes.ComponentContractInvalid, message);
}
