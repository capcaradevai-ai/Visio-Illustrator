using VisioBridge.Core;
using VisioBridge.Core.Shapes;

namespace VisioBridge.Tests;

public sealed class StyleMapperTests
{
    [Theory]
    [InlineData("#112233", "RGB(17,34,51)")]
    [InlineData("#FFFFFF", "RGB(255,255,255)")]
    public void Hex_colors_become_controlled_RGB_formulas(string input, string expected)
    {
        Assert.Equal(expected, StyleMapper.ToRgbFormula(input));
    }

    [Theory]
    [InlineData("red")]
    [InlineData("#12345")]
    [InlineData("#GG0000")]
    public void Invalid_colors_are_rejected(string input)
    {
        var error = Assert.Throws<BridgeCommandException>(() =>
            StyleMapper.ToRgbFormula(input));

        Assert.Equal(BridgeErrorCodes.InvalidStyle, error.Code);
    }
}
