using System.Text.Json.Serialization;

namespace VisioTestBridge.Contracts;

public sealed record MatrixComponentContract(
    CompositeComponentContract Component,
    IReadOnlyList<IReadOnlyList<double>> Values,
    bool Heatmap = false,
    [property: JsonPropertyName("color_scale")] MatrixColorScale? ColorScale = null,
    [property: JsonPropertyName("row_labels")] IReadOnlyList<string>? RowLabels = null,
    [property: JsonPropertyName("column_labels")] IReadOnlyList<string>? ColumnLabels = null);

public sealed record MatrixColorScale(
    double Minimum,
    double Maximum,
    [property: JsonPropertyName("minimum_color")] string MinimumColor,
    [property: JsonPropertyName("maximum_color")] string MaximumColor);

public sealed record GridComponentContract(
    CompositeComponentContract Component,
    int Rows,
    int Columns,
    IReadOnlyList<IReadOnlyList<string>>? Labels,
    IReadOnlyList<GridCellAddress>? Highlights = null,
    [property: JsonPropertyName("row_labels")] IReadOnlyList<string>? RowLabels = null,
    [property: JsonPropertyName("column_labels")] IReadOnlyList<string>? ColumnLabels = null);

public sealed record GridCellAddress(int Row, int Column);
