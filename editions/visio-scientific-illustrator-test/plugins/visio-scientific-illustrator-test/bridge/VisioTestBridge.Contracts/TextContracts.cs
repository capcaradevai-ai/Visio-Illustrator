using System.Text.Json.Serialization;

namespace VisioTestBridge.Contracts;

public sealed record RichText(
    string Text,
    TextParagraphStyle? Paragraph = null,
    IReadOnlyList<TextRun>? Runs = null,
    string Fit = "none",
    [property: JsonPropertyName("minimum_font_size_pt")] double? MinimumFontSizePt = null);

public sealed record TextRun(
    int Start,
    int Length,
    [property: JsonPropertyName("font_family")] string? FontFamily = null,
    [property: JsonPropertyName("font_size_pt")] double? FontSizePt = null,
    string? Color = null,
    bool? Bold = null,
    bool? Italic = null,
    bool? Underline = null,
    bool? Strikethrough = null,
    bool? Superscript = null,
    bool? Subscript = null);

public sealed record TextParagraphStyle(
    [property: JsonPropertyName("horizontal_align")] string? HorizontalAlign = null,
    [property: JsonPropertyName("vertical_align")] string? VerticalAlign = null,
    [property: JsonPropertyName("left_margin_pt")] double? LeftMarginPt = null,
    [property: JsonPropertyName("right_margin_pt")] double? RightMarginPt = null,
    [property: JsonPropertyName("top_margin_pt")] double? TopMarginPt = null,
    [property: JsonPropertyName("bottom_margin_pt")] double? BottomMarginPt = null,
    [property: JsonPropertyName("line_spacing_pt")] double? LineSpacingPt = null,
    [property: JsonPropertyName("space_before_pt")] double? SpaceBeforePt = null,
    [property: JsonPropertyName("space_after_pt")] double? SpaceAfterPt = null);
