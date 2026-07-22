using System.Text.Json.Serialization;

namespace VisioBridge.Contracts;

public sealed record FillStyle(
    string Kind,
    string? Color = null,
    [property: JsonPropertyName("secondary_color")] string? SecondaryColor = null,
    [property: JsonPropertyName("transparency_percent")] double? TransparencyPercent = null,
    [property: JsonPropertyName("secondary_transparency_percent")] double? SecondaryTransparencyPercent = null,
    [property: JsonPropertyName("gradient_angle_deg")] double? GradientAngleDeg = null,
    [property: JsonPropertyName("gradient_direction")] int? GradientDirection = null,
    int? Pattern = null);

public sealed record LineStyle(
    string? Color = null,
    [property: JsonPropertyName("width_pt")] double? WidthPt = null,
    [property: JsonPropertyName("transparency_percent")] double? TransparencyPercent = null,
    int? Pattern = null,
    [property: JsonPropertyName("begin_arrow")] string? BeginArrow = null,
    [property: JsonPropertyName("end_arrow")] string? EndArrow = null,
    [property: JsonPropertyName("begin_arrow_size")] string? BeginArrowSize = null,
    [property: JsonPropertyName("end_arrow_size")] string? EndArrowSize = null,
    string? Cap = null,
    string? Join = null,
    bool? Rounded = null);

public sealed record TextBlockStyle(
    [property: JsonPropertyName("horizontal_align")] string? HorizontalAlign = null,
    [property: JsonPropertyName("vertical_align")] string? VerticalAlign = null,
    [property: JsonPropertyName("left_margin_pt")] double? LeftMarginPt = null,
    [property: JsonPropertyName("right_margin_pt")] double? RightMarginPt = null,
    [property: JsonPropertyName("top_margin_pt")] double? TopMarginPt = null,
    [property: JsonPropertyName("bottom_margin_pt")] double? BottomMarginPt = null);

public sealed record ShadowStyle(
    bool Enabled,
    string? Color = null,
    [property: JsonPropertyName("transparency_percent")] double? TransparencyPercent = null,
    [property: JsonPropertyName("offset_x_pt")] double? OffsetXPt = null,
    [property: JsonPropertyName("offset_y_pt")] double? OffsetYPt = null);

public sealed record CornerStyle(
    [property: JsonPropertyName("radius_pt")] double RadiusPt);
