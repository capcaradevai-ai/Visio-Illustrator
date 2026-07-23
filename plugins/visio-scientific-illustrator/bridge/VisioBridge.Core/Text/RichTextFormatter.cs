using System.Runtime.InteropServices;
using System.Globalization;
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

internal sealed record CompiledRichTextPlan(
    CompiledTextRun? Common,
    IReadOnlyList<CompiledTextRun> Runs);

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

    public static CompiledRichTextPlan CompilePlan(RichText richText)
    {
        var runs = Compile(richText);
        if (!CoversWholeText(runs, richText.Text.Length)) return new CompiledRichTextPlan(null, runs);

        var font = Common(runs.Select(run => run.FontFamily));
        var size = Common(runs.Select(run => run.FontSizePt));
        var color = Common(runs.Select(run => run.Color));
        var common = font is null && size is null && color is null
            ? null
            : new CompiledTextRun(0, richText.Text.Length, font, size, color, 0, 0, null);
        var optimized = runs.Select(run => run with
        {
            FontFamily = font is null ? run.FontFamily : null,
            FontSizePt = size is null ? run.FontSizePt : null,
            Color = color is null ? run.Color : null,
        }).ToArray();
        return new CompiledRichTextPlan(common, optimized);
    }

    private static bool CoversWholeText(IReadOnlyList<CompiledTextRun> runs, int length)
    {
        if (runs.Count == 0 || runs[0].Start != 0) return false;
        var end = 0;
        foreach (var run in runs)
        {
            if (run.Start != end) return false;
            end = run.Start + run.Length;
        }
        return end == length;
    }

    private static T? Common<T>(IEnumerable<T?> values)
    {
        var items = values.ToArray();
        if (items.Length == 0 || items[0] is null) return default;
        return items.All(item => EqualityComparer<T?>.Default.Equals(item, items[0]))
            ? items[0]
            : default;
    }

    private static void Invalid(string message) =>
        throw new BridgeCommandException(BridgeErrorCodes.InvalidStyle, message);
}

internal static class RichTextFormatter
{
    public static void Apply(Visio.Shape shape, RichText richText)
    {
        var plan = RichTextCompiler.CompilePlan(richText);
        shape.Text = richText.Text;
        ApplyParagraph(shape, richText.Paragraph);
        if (plan.Common is not null) ApplyRun(shape, plan.Common);
        foreach (var run in plan.Runs.Where(RequiresComWrite)) ApplyRun(shape, run);
        ApplyFit(shape, richText);
    }

    private static bool RequiresComWrite(CompiledTextRun run) =>
        run.FontFamily is not null
        || run.FontSizePt is not null
        || run.Color is not null
        || run.StyleBits != 0
        || run.Position != 0
        || run.Strikethrough == true;

    private static void ApplyRun(Visio.Shape shape, CompiledTextRun run)
    {
        Visio.Characters? characters = null;
        try
        {
            characters = shape.Characters;
            characters.Begin = run.Start;
            characters.End = run.Start + run.Length;
            if (run.StyleBits != 0)
                characters.set_CharProps((short)Visio.VisCellIndices.visCharacterStyle, (short)run.StyleBits);
            if (run.Position != 0)
                characters.set_CharProps((short)Visio.VisCellIndices.visCharacterPos, (short)run.Position);
            if (run.FontSizePt is not null)
                ApplyRunFontSize(shape, characters, run.FontSizePt.Value);
            if (run.Strikethrough == true)
                characters.set_CharProps((short)Visio.VisCellIndices.visCharacterStrikethru, 1);
            if (run.FontFamily is not null) ApplyFont(shape, characters, run.FontFamily);
            if (run.Color is not null) ApplyRunColor(shape, characters, run.Color);
        }
        finally
        {
            if (characters is not null) Marshal.FinalReleaseComObject(characters);
        }
    }

    private static void ApplyRunFontSize(Visio.Shape shape, Visio.Characters characters, double sizePt)
    {
        var row = characters.get_CharPropsRow((short)Visio.VisCharsBias.visBiasLetVisioChoose);
        if (row < 0)
            throw new BridgeCommandException(BridgeErrorCodes.InvalidStyle, "无法为跨格式文本区间设置字号。");
        Visio.Cell? cell = null;
        try
        {
            cell = shape.CellsSRC[
                (short)Visio.VisSectionIndices.visSectionCharacter,
                (short)row,
                (short)Visio.VisCellIndices.visCharacterSize];
            cell.FormulaU = ShapeSheetValue.Points(sizePt);
        }
        finally
        {
            if (cell is not null) Marshal.FinalReleaseComObject(cell);
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
        else if (richText.Fit == "shrink_text")
        {
            ShrinkTextToFit(shape, richText.MinimumFontSizePt ?? 5);
        }
    }

    private static void ShrinkTextToFit(Visio.Shape shape, double minimumFontSizePt)
    {
        var textWidth = Math.Max(0.001, ReadResultIU(shape, "TxtWidth")
            - ReadResultIU(shape, "LeftMargin") - ReadResultIU(shape, "RightMargin"));
        var textHeight = Math.Max(0.001, ReadResultIU(shape, "TxtHeight"));
        for (var iteration = 0; iteration < 3; iteration++)
        {
            var measuredWidth = MeasureText(shape, "TEXTWIDTH(TheText)");
            var measuredHeight = MeasureText(
                shape,
                $"TEXTHEIGHT(TheText,{textWidth.ToString("R", CultureInfo.InvariantCulture)} in)");
            var scale = Math.Min(1, Math.Min(textWidth / Math.Max(0.001, measuredWidth), textHeight / Math.Max(0.001, measuredHeight)));
            if (scale >= 0.995) return;
            if (!ScaleCharacterRows(shape, scale, minimumFontSizePt)) return;
        }
    }

    private static double MeasureText(Visio.Shape shape, string formula)
    {
        var section = (short)Visio.VisSectionIndices.visSectionUser;
        if (shape.SectionExists[section, (short)Visio.VisExistsFlags.visExistsAnywhere] == 0)
            shape.AddSection(section);
        var row = shape.AddNamedRow(section, "CodexTextMeasure", (short)Visio.VisRowTags.visTagDefault);
        Visio.Cell? cell = null;
        try
        {
            cell = shape.CellsU["User.CodexTextMeasure"];
            cell.FormulaU = formula;
            return cell.ResultIU;
        }
        finally
        {
            if (cell is not null) Marshal.FinalReleaseComObject(cell);
            shape.DeleteRow(section, row);
        }
    }

    private static bool ScaleCharacterRows(Visio.Shape shape, double scale, double minimumFontSizePt)
    {
        var section = (short)Visio.VisSectionIndices.visSectionCharacter;
        var changed = false;
        for (short row = 0; row < shape.RowCount[section]; row++)
        {
            Visio.Cell? cell = null;
            try
            {
                cell = shape.CellsSRC[section, row, (short)Visio.VisCellIndices.visCharacterSize];
                var current = cell.get_Result(Visio.VisUnitCodes.visPoints);
                var next = Math.Max(minimumFontSizePt, current * scale);
                if (next >= current - 0.001) continue;
                cell.FormulaU = ShapeSheetValue.Points(next);
                changed = true;
            }
            finally
            {
                if (cell is not null) Marshal.FinalReleaseComObject(cell);
            }
        }
        return changed;
    }

    private static double ReadResultIU(Visio.Shape shape, string cellName)
    {
        Visio.Cell? cell = null;
        try
        {
            cell = shape.CellsU[cellName];
            return cell.ResultIU;
        }
        finally
        {
            if (cell is not null) Marshal.FinalReleaseComObject(cell);
        }
    }
}
