using System.Runtime.InteropServices;
using System.Text.Json;
using VisioBridge.Core;
using VisioBridge.Core.Interop;

namespace VisioBridge.Tests;

public sealed class ComExceptionTranslatorTests
{
    [Theory]
    [InlineData(unchecked((int)0x80010001))]
    [InlineData(unchecked((int)0x8001010A))]
    public void Busy_hresult_is_retryable(int hresult)
    {
        var translated = ComExceptionTranslator.Translate(
            new COMException("busy", hresult));

        Assert.Equal(BridgeErrorCodes.VisioBusy, translated.Code);
        Assert.True(translated.Retryable);
    }

    [Fact]
    public void Unrelated_hresult_remains_internal_and_preserves_hresult()
    {
        const int hresult = unchecked((int)0x80004005);

        var translated = ComExceptionTranslator.Translate(
            new COMException("unexpected", hresult));
        var details = JsonSerializer.SerializeToElement(translated.Details);

        Assert.Equal(BridgeErrorCodes.InternalError, translated.Code);
        Assert.False(translated.Retryable);
        Assert.Equal(hresult, details.GetProperty("hresult").GetInt32());
    }
}
