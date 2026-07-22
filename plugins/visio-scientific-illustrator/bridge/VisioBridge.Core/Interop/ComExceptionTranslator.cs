using System.Runtime.InteropServices;

namespace VisioBridge.Core.Interop;

public static class ComExceptionTranslator
{
    private const int RpcCallRejected = unchecked((int)0x80010001);
    private const int RpcServerCallRetryLater = unchecked((int)0x8001010A);

    public static BridgeCommandException Translate(COMException error)
    {
        ArgumentNullException.ThrowIfNull(error);
        var busy = error.HResult is RpcCallRejected or RpcServerCallRetryLater;
        return new BridgeCommandException(
            busy ? BridgeErrorCodes.VisioBusy : BridgeErrorCodes.InternalError,
            busy
                ? "Microsoft Visio is busy. Retry the operation after Visio finishes its current interaction."
                : "Microsoft Visio returned an unexpected COM error.",
            retryable: busy,
            details: new { hresult = error.HResult });
    }
}
