using System.Globalization;
using System.Text.Json;
using VisioTestBridge.Contracts;

namespace VisioTestBridge.Core.Components;

internal static class ChartComponentPlanner
{
    private static readonly HashSet<string> ChartTypes =
        new(["bar", "line", "scatter", "error_bar"], StringComparer.Ordinal);

    public static CompositeComponentPlan CreatePlan(ChartComponentContract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(contract.Component);
        ArgumentNullException.ThrowIfNull(contract.Series);
        ArgumentNullException.ThrowIfNull(contract.XAxis);
        ArgumentNullException.ThrowIfNull(contract.YAxis);
        ArgumentNullException.ThrowIfNull(contract.Legend);
        CompositeComponentPlan.ValidateContract(contract.Component);
        if (!string.Equals(contract.Component.ComponentType, "chart", StringComparison.Ordinal)
            || !ChartTypes.Contains(contract.ChartType))
            throw Invalid("图表合同的 component_type 或 chart_type 无效。");
        if (contract.Series.Count is < 1 or > 20)
            throw Invalid("图表必须包含 1–20 个 series。");
        if (contract.XAxis.TickCount is < 2 or > 20 || contract.YAxis.TickCount is < 2 or > 20)
            throw Invalid("坐标轴 tick_count 必须为 2–20。");
        if (contract.Legend.Position is not ("right" or "bottom" or "none"))
            throw Invalid("legend position 必须是 right、bottom 或 none。");

        foreach (var series in contract.Series)
        {
            if (string.IsNullOrWhiteSpace(series.Name)
                || series.X.Count == 0
                || series.X.Count != series.Y.Count
                || series.X.Any(value => !double.IsFinite(value))
                || series.Y.Any(value => !double.IsFinite(value))
                || series.Error is not null
                && (series.Error.Count != series.Y.Count
                    || series.Error.Any(value => !double.IsFinite(value) || value < 0))
                || contract.ChartType == "error_bar" && series.Error is null)
            {
                throw Invalid("series 名称、长度、有限数值或误差值无效。");
            }
        }

        var xDomain = ResolveDomain(
            contract.XAxis,
            contract.Series.SelectMany(series => series.X));
        var yValues = contract.Series.SelectMany(series =>
            series.Error is null
                ? series.Y
                : series.Y.SelectMany((value, index) =>
                    new[] { value - series.Error[index], value + series.Error[index] }));
        var yDomain = ResolveDomain(contract.YAxis, yValues);
        var definitions = new List<CompositePrimitiveDefinition>();
        var bounds = contract.Component.Bounds;
        var legendWidth = contract.Legend.Show && contract.Legend.Position == "right" ? 90d : 0d;
        var plot = new PixelBox(
            bounds.X + 52,
            bounds.Y + 30,
            Math.Max(40, bounds.Width - 72 - legendWidth),
            Math.Max(40, bounds.Height - 82));

        AddAxes(definitions, contract, plot, xDomain, yDomain);
        AddSeries(definitions, contract, plot, xDomain, yDomain);
        AddLegend(definitions, contract, plot);
        if (!string.IsNullOrWhiteSpace(contract.Title))
            definitions.Add(Text(
                "title",
                new PixelBox(bounds.X, bounds.Y, bounds.Width, 24),
                contract.Title,
                bold: true));

        return CompositeComponentPlan.Create(contract.Component, definitions);
    }

    private static void AddAxes(
        ICollection<CompositePrimitiveDefinition> result,
        ChartComponentContract contract,
        PixelBox plot,
        Domain xDomain,
        Domain yDomain)
    {
        result.Add(Path(
            "axis.x",
            new PixelBox(plot.X, plot.Y + plot.Height, plot.Width, 1),
            new { axis = "x", domain_min = xDomain.Minimum, domain_max = xDomain.Maximum }));
        result.Add(Path(
            "axis.y",
            new PixelBox(plot.X, plot.Y, 1, plot.Height),
            new { axis = "y", domain_min = yDomain.Minimum, domain_max = yDomain.Maximum }));
        result.Add(Text(
            "axis.x.title",
            new PixelBox(plot.X, plot.Y + plot.Height + 30, plot.Width, 20),
            contract.XAxis.Label));
        result.Add(Text(
            "axis.y.title",
            new PixelBox(plot.X - 48, plot.Y, 20, plot.Height),
            contract.YAxis.Label,
            rotation: -90));
        AddTicks(result, "x", contract.XAxis.TickCount, plot, xDomain);
        AddTicks(result, "y", contract.YAxis.TickCount, plot, yDomain);
    }

    private static void AddTicks(
        ICollection<CompositePrimitiveDefinition> result,
        string axis,
        int count,
        PixelBox plot,
        Domain domain)
    {
        for (var index = 0; index < count; index++)
        {
            var ratio = index / (double)(count - 1);
            var value = domain.Minimum + ratio * (domain.Maximum - domain.Minimum);
            var tickBounds = axis == "x"
                ? new PixelBox(plot.X + ratio * plot.Width, plot.Y + plot.Height, 1, 6)
                : new PixelBox(plot.X - 6, plot.Y + (1 - ratio) * plot.Height, 6, 1);
            var labelBounds = axis == "x"
                ? new PixelBox(tickBounds.X - 24, tickBounds.Y + 7, 48, 16)
                : new PixelBox(plot.X - 46, tickBounds.Y - 8, 36, 16);
            result.Add(Path($"axis.{axis}.tick.{index}", tickBounds, new { axis, value }));
            result.Add(Text(
                $"axis.{axis}.tick.{index}.label",
                labelBounds,
                value.ToString("0.###", CultureInfo.InvariantCulture)));
        }
    }

    private static void AddSeries(
        ICollection<CompositePrimitiveDefinition> result,
        ChartComponentContract contract,
        PixelBox plot,
        Domain xDomain,
        Domain yDomain)
    {
        for (var seriesIndex = 0; seriesIndex < contract.Series.Count; seriesIndex++)
        {
            var series = contract.Series[seriesIndex];
            var seriesId = Slug(series.Name);
            var points = series.X.Select((x, index) => new PixelPoint(
                Map(x, xDomain, plot.X, plot.X + plot.Width),
                Map(series.Y[index], yDomain, plot.Y + plot.Height, plot.Y)))
                .ToArray();

            if (contract.ChartType == "bar")
            {
                var width = Math.Max(3, plot.Width / Math.Max(1, series.X.Count * contract.Series.Count) * 0.7);
                var baseline = Map(
                    Math.Clamp(0, yDomain.Minimum, yDomain.Maximum),
                    yDomain,
                    plot.Y + plot.Height,
                    plot.Y);
                for (var index = 0; index < points.Length; index++)
                {
                    var top = Math.Min(points[index].Y, baseline);
                    result.Add(Shape(
                        $"series.{seriesId}.bar.{index}",
                        new PixelBox(points[index].X - width / 2, top, width,
                            Math.Max(1, Math.Abs(baseline - points[index].Y))),
                        new { series = series.Name, index, fill = series.Color }));
                }
            }
            else
            {
                if (contract.ChartType is "line" or "error_bar")
                {
                    for (var index = 1; index < points.Length; index++)
                    {
                        var left = Math.Min(points[index - 1].X, points[index].X);
                        var top = Math.Min(points[index - 1].Y, points[index].Y);
                        result.Add(Path(
                            $"series.{seriesId}.segment.{index - 1}",
                            new PixelBox(
                                left,
                                top,
                                Math.Max(1, Math.Abs(points[index].X - points[index - 1].X)),
                                Math.Max(1, Math.Abs(points[index].Y - points[index - 1].Y))),
                            new
                            {
                                series = series.Name,
                                from = index - 1,
                                to = index,
                                stroke = series.Color,
                                start_y_percent = points[index - 1].Y <= points[index].Y ? 0 : 100,
                                end_y_percent = points[index - 1].Y <= points[index].Y ? 100 : 0,
                            }));
                    }
                }
                for (var index = 0; index < points.Length; index++)
                {
                    result.Add(Shape(
                        $"series.{seriesId}.point.{index}",
                        new PixelBox(points[index].X - 3, points[index].Y - 3, 6, 6),
                        new { series = series.Name, index, fill = series.Color, shape = "ellipse" }));
                    if (series.Error is not null)
                    {
                        var upper = Map(
                            series.Y[index] + series.Error[index],
                            yDomain,
                            plot.Y + plot.Height,
                            plot.Y);
                        var lower = Map(
                            series.Y[index] - series.Error[index],
                            yDomain,
                            plot.Y + plot.Height,
                            plot.Y);
                        result.Add(Path(
                            $"series.{seriesId}.error.{index}",
                            new PixelBox(points[index].X, Math.Min(upper, lower), 1,
                                Math.Max(1, Math.Abs(lower - upper))),
                            new { series = series.Name, index, stroke = series.Color }));
                    }
                }
            }
        }
    }

    private static void AddLegend(
        ICollection<CompositePrimitiveDefinition> result,
        ChartComponentContract contract,
        PixelBox plot)
    {
        if (!contract.Legend.Show || contract.Legend.Position == "none")
            return;
        for (var index = 0; index < contract.Series.Count; index++)
        {
            var series = contract.Series[index];
            var id = Slug(series.Name);
            var x = contract.Legend.Position == "right"
                ? plot.X + plot.Width + 14
                : plot.X + index * 100;
            var y = contract.Legend.Position == "right"
                ? plot.Y + index * 22
                : plot.Y + plot.Height + 52;
            result.Add(Shape(
                $"legend.{id}.swatch",
                new PixelBox(x, y + 4, 12, 8),
                new { fill = series.Color, series = series.Name }));
            result.Add(Text(
                $"legend.{id}.text",
                new PixelBox(x + 16, y, 70, 18),
                series.Name));
        }
    }

    private static Domain ResolveDomain(ChartAxis axis, IEnumerable<double> values)
    {
        var array = values.ToArray();
        if (array.Length == 0 || array.Any(value => !double.IsFinite(value)))
            throw Invalid("坐标轴 domain 没有有限数据。");
        var minimum = axis.Minimum ?? array.Min();
        var maximum = axis.Maximum ?? array.Max();
        if (!double.IsFinite(minimum) || !double.IsFinite(maximum) || minimum > maximum)
            throw Invalid("坐标轴 domain 无效。");
        if (minimum == maximum)
        {
            var delta = Math.Abs(minimum) * 0.05;
            if (delta == 0) delta = 0.05;
            minimum -= delta;
            maximum += delta;
        }
        return new Domain(minimum, maximum);
    }

    private static double Map(double value, Domain domain, double start, double end) =>
        start + (value - domain.Minimum) / (domain.Maximum - domain.Minimum) * (end - start);

    private static CompositePrimitiveDefinition Shape(string path, PixelBox bounds, object data) =>
        Definition(path, "shape", bounds, data);

    private static CompositePrimitiveDefinition Path(string path, PixelBox bounds, object data) =>
        Definition(path, "path", bounds, data);

    private static CompositePrimitiveDefinition Text(
        string path,
        PixelBox bounds,
        string text,
        bool bold = false,
        double rotation = 0) =>
        Shape(path, bounds, new { text, bold, rotation, shape = "text" });

    private static CompositePrimitiveDefinition Definition(
        string path,
        string kind,
        PixelBox bounds,
        object data)
    {
        var element = JsonSerializer.SerializeToElement(data, ProtocolJson.Options);
        using var document = JsonDocument.Parse(element.GetRawText());
        var dictionary = document.RootElement.EnumerateObject()
            .ToDictionary(item => item.Name, item => item.Value.Clone(), StringComparer.Ordinal);
        dictionary["editable"] = JsonSerializer.SerializeToElement(true);
        return new CompositePrimitiveDefinition(
            path,
            kind,
            bounds,
            JsonSerializer.SerializeToElement(dictionary, ProtocolJson.Options));
    }

    private static string Slug(string value)
    {
        var characters = value.ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '-')
            .ToArray();
        var result = new string(characters).Trim('-');
        return result.Length == 0 ? "series" : result;
    }

    private static BridgeCommandException Invalid(string message) =>
        new(BridgeErrorCodes.ComponentContractInvalid, message);

    private sealed record Domain(double Minimum, double Maximum);
}
