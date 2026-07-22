using VisioBridge.Contracts;
using VisioBridge.Core;
using VisioBridge.Core.Text;

namespace VisioBridge.Tests;

public sealed class RichTextFormatterTests
{
    [Fact]
    public void Scientific_notation_runs_are_validated_and_compiled()
    {
        var text = new RichText(
            "A_l^i = log(|H_l| + ε)",
            new TextParagraphStyle("center", "middle"),
            [
                new TextRun(0, 1, FontFamily: "Cambria Math", FontSizePt: 14, Italic: true),
                new TextRun(2, 1, Subscript: true),
                new TextRun(4, 1, Superscript: true),
                new TextRun(13, 1, Italic: true),
                new TextRun(15, 1, Subscript: true),
            ]);

        var runs = RichTextCompiler.Compile(text);

        Assert.Equal(5, runs.Count);
        Assert.Equal(2, runs[1].Position);
        Assert.Equal(1, runs[2].Position);
        Assert.True((runs[0].StyleBits & 2) != 0);
        Assert.Equal("Cambria Math", runs[0].FontFamily);
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(0, 0)]
    [InlineData(4, 2)]
    public void Invalid_character_ranges_are_rejected(int start, int length)
    {
        var text = new RichText("abcd", Runs: [new TextRun(start, length, Bold: true)]);

        var error = Assert.Throws<BridgeCommandException>(() => RichTextCompiler.Compile(text));

        Assert.Equal(BridgeErrorCodes.InvalidStyle, error.Code);
    }

    [Fact]
    public void Overlapping_runs_are_rejected()
    {
        var text = new RichText("abcdef", Runs: [
            new TextRun(0, 4, Bold: true),
            new TextRun(3, 2, Italic: true),
        ]);

        Assert.Throws<BridgeCommandException>(() => RichTextCompiler.Compile(text));
    }

    [Fact]
    public void Superscript_and_subscript_are_mutually_exclusive()
    {
        var text = new RichText("x", Runs: [new TextRun(0, 1, Superscript: true, Subscript: true)]);

        Assert.Throws<BridgeCommandException>(() => RichTextCompiler.Compile(text));
    }

    [Fact]
    public void Text_fit_mode_is_strict()
    {
        Assert.Throws<BridgeCommandException>(() =>
            RichTextCompiler.Compile(new RichText("x", Fit: "automatic")));
        Assert.Single(RichTextCompiler.Compile(new RichText(
            "x", Runs: [new TextRun(0, 1, Bold: true)], Fit: "shrink_text")));
    }
}
