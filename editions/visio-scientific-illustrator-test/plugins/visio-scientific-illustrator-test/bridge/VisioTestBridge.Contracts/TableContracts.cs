using System.Text.Json.Serialization;

namespace VisioTestBridge.Contracts;

public sealed record TableComponentContract(
    CompositeComponentContract Component,
    IReadOnlyList<IReadOnlyList<TableCell>> Cells,
    TableLayout Layout,
    [property: JsonPropertyName("merged_cells")]
    IReadOnlyList<TableMergedCell>? MergedCells = null);

public sealed record TableCell(
    string Text,
    string? Fill = null,
    [property: JsonPropertyName("text_color")] string? TextColor = null,
    [property: JsonPropertyName("font_size")] double? FontSize = null,
    bool Bold = false);

public sealed record TableLayout(
    [property: JsonPropertyName("header_row")] bool HeaderRow = false,
    [property: JsonPropertyName("header_column")] bool HeaderColumn = false,
    [property: JsonPropertyName("row_heights")]
    IReadOnlyList<double>? RowHeights = null,
    [property: JsonPropertyName("column_widths")]
    IReadOnlyList<double>? ColumnWidths = null,
    [property: JsonPropertyName("horizontal_alignment")]
    string HorizontalAlignment = "center",
    [property: JsonPropertyName("vertical_alignment")]
    string VerticalAlignment = "middle");

public sealed record TableMergedCell(
    int Row,
    int Column,
    [property: JsonPropertyName("row_span")] int RowSpan,
    [property: JsonPropertyName("column_span")] int ColumnSpan);
