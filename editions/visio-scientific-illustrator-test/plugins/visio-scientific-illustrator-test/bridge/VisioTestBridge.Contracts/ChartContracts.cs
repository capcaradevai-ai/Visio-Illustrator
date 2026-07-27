using System.Text.Json.Serialization;

namespace VisioTestBridge.Contracts;

public sealed record ChartComponentContract(
    CompositeComponentContract Component,
    [property: JsonPropertyName("chart_type")] string ChartType,
    IReadOnlyList<ChartSeries> Series,
    [property: JsonPropertyName("x_axis")] ChartAxis XAxis,
    [property: JsonPropertyName("y_axis")] ChartAxis YAxis,
    ChartLegend Legend,
    string? Title = null);

public sealed record ChartSeries(
    string Name,
    IReadOnlyList<double> X,
    IReadOnlyList<double> Y,
    IReadOnlyList<double>? Error = null,
    string Color = "#2F75B5");

public sealed record ChartAxis(
    string Label,
    double? Minimum,
    double? Maximum,
    [property: JsonPropertyName("tick_count")] int TickCount = 5);

public sealed record ChartLegend(
    bool Show = true,
    string Position = "right");
