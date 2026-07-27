using VisioTestBridge.Contracts;

namespace VisioTestBridge.Core.Styling;

internal static class FillStyleMapper
{
    public static void Compile(FillStyle? style, IDictionary<string, string> cells)
    {
        if (style is null) return;
        switch (style.Kind)
        {
            case "none":
                RejectColorForNone(style);
                cells["FillPattern"] = "0";
                return;
            case "solid":
                RequirePrimaryColor(style);
                cells["FillPattern"] = "1";
                break;
            case "gradient":
                RequirePrimaryColor(style);
                if (style.SecondaryColor is null)
                    throw ShapeSheetValue.InvalidStyle("gradient fill 要求 secondary_color。");
                cells["FillPattern"] = "1";
                cells["FillBkgnd"] = ShapeSheetValue.Color(style.SecondaryColor);
                cells["FillGradientDir"] = (style.GradientDirection ?? 0).ToString();
                if (style.GradientAngleDeg is not null)
                    cells["FillGradientAngle"] = ShapeSheetValue.Degrees(style.GradientAngleDeg.Value);
                break;
            case "pattern":
                RequirePrimaryColor(style);
                if (style.Pattern is null or < 2 or > 40)
                    throw ShapeSheetValue.InvalidStyle("pattern fill 要求 2–40 的 pattern。");
                cells["FillPattern"] = style.Pattern.Value.ToString();
                if (style.SecondaryColor is not null) cells["FillBkgnd"] = ShapeSheetValue.Color(style.SecondaryColor);
                break;
            default:
                throw ShapeSheetValue.InvalidStyle("fill.kind 仅允许 none、solid、gradient 或 pattern。");
        }

        cells["FillForegnd"] = ShapeSheetValue.Color(style.Color!);
        if (style.TransparencyPercent is not null)
        {
            ShapeSheetValue.Range(style.TransparencyPercent.Value, 0, 100, "fill.transparency_percent");
            cells["FillForegndTrans"] = ShapeSheetValue.Percent(style.TransparencyPercent.Value);
        }
        if (style.SecondaryTransparencyPercent is not null)
        {
            ShapeSheetValue.Range(style.SecondaryTransparencyPercent.Value, 0, 100, "fill.secondary_transparency_percent");
            cells["FillBkgndTrans"] = ShapeSheetValue.Percent(style.SecondaryTransparencyPercent.Value);
        }
    }

    private static void RequirePrimaryColor(FillStyle style)
    {
        if (style.Color is null) throw ShapeSheetValue.InvalidStyle($"{style.Kind} fill 要求 color。");
    }

    private static void RejectColorForNone(FillStyle style)
    {
        if (style.Color is not null || style.SecondaryColor is not null || style.Pattern is not null)
            throw ShapeSheetValue.InvalidStyle("none fill 不能包含颜色或 pattern。");
    }
}
