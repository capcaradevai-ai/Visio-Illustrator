using VisioBridge.Contracts;

namespace VisioBridge.Core.Styling;

internal static class LineStyleMapper
{
    private static readonly IReadOnlyDictionary<string, string> Arrows = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["none"] = "0",
        ["open"] = "4",
        ["triangle"] = "5",
        ["diamond"] = "10",
        ["oval"] = "13",
    };

    private static readonly IReadOnlyDictionary<string, string> ArrowSizes = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["small"] = "2",
        ["medium"] = "3",
        ["large"] = "4",
    };

    public static void Compile(LineStyle? style, IDictionary<string, string> cells)
    {
        if (style is null) return;
        if (style.Color is not null) cells["LineColor"] = ShapeSheetValue.Color(style.Color);
        if (style.WidthPt is not null)
        {
            ShapeSheetValue.Range(style.WidthPt.Value, 0, 50, "line.width_pt");
            cells["LineWeight"] = ShapeSheetValue.Points(style.WidthPt.Value);
        }
        if (style.TransparencyPercent is not null)
        {
            ShapeSheetValue.Range(style.TransparencyPercent.Value, 0, 100, "line.transparency_percent");
            cells["LineColorTrans"] = ShapeSheetValue.Percent(style.TransparencyPercent.Value);
        }
        if (style.Pattern is not null)
        {
            ShapeSheetValue.Range(style.Pattern.Value, 0, 23, "line.pattern");
            cells["LinePattern"] = style.Pattern.Value.ToString();
        }
        AddEnum(cells, "BeginArrow", style.BeginArrow, Arrows, "begin_arrow");
        AddEnum(cells, "EndArrow", style.EndArrow, Arrows, "end_arrow");
        AddEnum(cells, "BeginArrowSize", style.BeginArrowSize, ArrowSizes, "begin_arrow_size");
        AddEnum(cells, "EndArrowSize", style.EndArrowSize, ArrowSizes, "end_arrow_size");
        if (style.Cap is not null) cells["LineCap"] = style.Cap switch
        {
            "square" => "0", "round" => "1", "extended" => "2",
            _ => throw ShapeSheetValue.InvalidStyle("line.cap 无效。"),
        };
        if (style.Join is not null) cells["LineJoin"] = style.Join switch
        {
            "round" => "0", "bevel" => "1", "miter" => "2",
            _ => throw ShapeSheetValue.InvalidStyle("line.join 无效。"),
        };
        if (style.Rounded is not null) cells["Rounding"] = style.Rounded.Value ? "4 pt" : "0 pt";
    }

    private static void AddEnum(
        IDictionary<string, string> cells,
        string cell,
        string? value,
        IReadOnlyDictionary<string, string> allowed,
        string name)
    {
        if (value is null) return;
        if (!allowed.TryGetValue(value, out var formula))
            throw ShapeSheetValue.InvalidStyle($"line.{name} 无效。");
        cells[cell] = formula;
    }
}
