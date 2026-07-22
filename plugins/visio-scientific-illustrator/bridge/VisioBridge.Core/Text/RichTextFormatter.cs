using System.Runtime.InteropServices;
using VisioBridge.Contracts;
using VisioBridge.Core.Shapes;
using VisioBridge.Core.Styling;
using Visio = Microsoft.Office.Interop.Visio;

namespace VisioBridge.Core.Text;

internal sealed record CompiledTextRun(
    int Start,
    int Length,
    string? FontFamily,
    double? FontSizePt,
    string? Color,
    int StyleBits,
    int Position,
    bool? Strikethrough);

internal static class RichTextCompiler
{
    private static readonly HashSet<string> FitModes = new(StringComparer.Ordinal)
    {
        "none", "shrink_text", "resize_shape",
    };

    public static IReadOnlyList<CompiledTextRun> Compile(RichText richText)
    {
        ArgumentNullException.ThrowIfNull(richText);
        if (!FitModes.Contains(richText.Fit)) Invalid("fit 仅允许 none、shrink_text 或 resize_shape。");
        if (richText.MinimumFontSizePt is not null)
            ShapeSheetValue.Range(richText.MinimumFontSizePt.Value, 1, 200, "minimum_font_size_pt");

        var result = new List<CompiledTextRun>();
        var previousEnd = -1;
        foreach (var run in (richText.Runs ?? []).OrderBy(run => run.Start))
        {
            if (run.Start < 0 || run.Length <= 0 || run.Start + run.Length > richText.Text.Length)
                Invalid("文本格式区间越界或长度无效。");
            if (run.Start < previousEnd) Invalid("文本格式区间不能重叠。");
            if (run.Superscript == true && run.Subscript == true) Invalid("上标和下标不能同时启用。");
            if (run.FontSizePt is not null) ShapeSheetValue.Range(run.FontSizePt.Value, 1, 200, "font_size_pt");
            if (run.Color is not null) _ = ShapeSheetValue.Color(run.Color);
            if (run.FontFamily is not null
                && (string.IsNullOrWhiteSpace(run.FontFamily)
                    || run.FontFamily.Contains('"')
                    || run.FontFamily.Contains('\r')
                    || run.FontFamily.Contains('\n')))
                Invalid("font_family 不能为空白或包含引号、换行符。");

            var style = 0;
            if (run.Bold == true) style |= 1;
            if (run.Italic == true) style |= 2;
            if (run.Underline == true) style |= 4;
            var position = run.Superscript == true ? 1 : run.Subscript == true ? 2 : 0;
            result.Add(new CompiledTextRun(
                run.Start,
                run.Length,
                run.FontFamily,
                run.FontSizePt,
                run.Color,
                style,
                position,
                run.Strikethrough));
            previousEnd = run.Start + run.Length;
        }
        return result;
    }

    private static void Invalid(string message) =>
        throw new BridgeCommandException(BridgeErrorCodes.InvalidStyle, message);
}

internal static class RichTextFormatter
{
    public static void Apply(Visio.Shape shape, RichText richText)
    {
        var runs = RichTextCompiler.Compile(richText);
        shape.Text = richText.Text;
        ApplyParagraph(shape, richText.Paragraph);
        foreach (var run in runs) ApplyRun(shape, run);
        ApplyFit(shape, richText);
    }

    private static void ApplyRun(Visio.Shape shape, CompiledTextRun run)
    {
        Visio.Characters? characters = null;
        try
        {
            characters = shape.Characters;
            characters.Begin = run.Start;
            characters.End = run.Start + run.Length;
            characters.set_CharProps((short)Visio.VisCellIndices.visCharacterStyle, (short)run.StyleBits);
            characters.set_CharProps((short)Visio.VisCellIndices.visCharacterPos, (short)run.Position);
            if (run.FontSizePt is not null)
                characters.set_CharProps((short)Visio.VisCellIndices.visCharacterSize, (short)Math.Round(run.FontSizePt.Value));
            if (run.Strikethrough is not null)
                characters.set_CharProps((short)Visio.VisCellIndices.visCharacterStrikethru, (short)(run.Strikethrough.Value ? 1 : 0));
            if (run.FontFamily is not null) ApplyFont(shape, characters, run.FontFamily);
            if (run.Color is not null) ApplyRunColor(shape, characters, run.Color);
        }
        finally
        {
            if (characters is not null) Marshal.FinalReleaseComObject(characters);
        }
    }

    private static void ApplyFont(Visio.Shape shape, Visio.Characters characters, string family)
    {
        var row = characters.get_CharPropsRow((short)Visio.VisCharsBias.visBiasLetVisioChoose);
        if (row < 0)
            throw new BridgeCommandException(BridgeErrorCodes.InvalidStyle, "无法为跨格式文本区间设置字体。");
        Visio.Cell? cell = null;
        try
        {
            cell = shape.CellsSRC[
                (short)Visio.VisSectionIndices.visSectionCharacter,
                (short)row,
                (short)Visio.VisCellIndices.visCharacterFont];
            cell.FormulaU = $"FONT(\"{family}\")";
        }
        catch (COMException exception)
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.InvalidStyle,
                $"Visio 中不可用字体: {family}",
                details: new { exception.HResult });
        }
        finally
        {
            if (cell is not null) Marshal.FinalReleaseComObject(cell);
        }
    }

    private static void ApplyRunColor(Visio.Shape shape, Visio.Characters characters, string color)
    {
        var row = characters.get_CharPropsRow((short)Visio.VisCharsBias.visBiasLetVisioChoose);
        if (row < 0)
            throw new BridgeCommandException(BridgeErrorCodes.InvalidStyle, "无法为跨格式文本区间设置颜色。");
        Visio.Cell? cell = null;
        try
        {
            cell = shape.CellsSRC[
                (short)Visio.VisSectionIndices.visSectionCharacter,
                (short)row,
                (short)Visio.VisCellIndices.visCharacterColor];
            cell.FormulaU = ShapeSheetValue.Color(color);
        }
        finally
        {
            if (cell is not null) Marshal.FinalReleaseComObject(cell);
        }
    }

    private static void ApplyParagraph(Visio.Shape shape, TextParagraphStyle? paragraph)
    {
        if (paragraph is null) return;
        var style = new ShapeStyle(TextBlock: new TextBlockStyle(
            paragraph.HorizontalAlign,
            paragraph.VerticalAlign,
            paragraph.LeftMarginPt,
            paragraph.RightMarginPt,
            paragraph.TopMarginPt,
            paragraph.BottomMarginPt));
        ShapeSheetWriter.Apply(shape, StyleFormulaCompiler.Compile(style));
        if (paragraph.LineSpacingPt is not null) StyleMapper.SetFormula(shape, "Para.SpLine", ShapeSheetValue.Points(paragraph.LineSpacingPt.Value));
        if (paragraph.SpaceBeforePt is not null) StyleMapper.SetFormula(shape, "Para.SpBefore", ShapeSheetValue.Points(paragraph.SpaceBeforePt.Value));
        if (paragraph.SpaceAfterPt is not null) StyleMapper.SetFormula(shape, "Para.SpAfter", ShapeSheetValue.Points(paragraph.SpaceAfterPt.Value));
    }

    private static void ApplyFit(Visio.Shape shape, RichText richText)
    {
        if (richText.Fit == "resize_shape")
        {
            StyleMapper.SetFormula(shape, "Width", "TEXTWIDTH(TheText)+LeftMargin+RightMargin");
            StyleMapper.SetFormula(shape, "Height", "TEXTHEIGHT(TheText,Width)+TopMargin+BottomMargin");
        }
    }
}
