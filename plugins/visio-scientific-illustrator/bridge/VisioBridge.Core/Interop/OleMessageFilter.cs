using System.Runtime.InteropServices;

namespace VisioBridge.Core.Interop;

public enum OleRejectType
{
    Rejected,
    RetryLater,
}

public sealed class OleRetryPolicy
{
    public OleRetryPolicy(int maxElapsedMs, int delayMs)
    {
        if (maxElapsedMs <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxElapsedMs));
        }
        if (delayMs <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(delayMs));
        }

        MaxElapsedMs = maxElapsedMs;
        DelayMs = delayMs;
    }

    public int MaxElapsedMs { get; }

    public int DelayMs { get; }

    public int GetRetryDelay(int elapsedMs, OleRejectType rejectType) =>
        rejectType == OleRejectType.RetryLater && elapsedMs < MaxElapsedMs
            ? DelayMs
            : -1;
}

[ComImport]
[Guid("00000016-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IOleMessageFilter
{
    [PreserveSig]
    int HandleInComingCall(
        int callType,
        IntPtr callerTask,
        int tickCount,
        IntPtr interfaceInfo);

    [PreserveSig]
    int RetryRejectedCall(
        IntPtr calleeTask,
        int tickCount,
        int rejectType);

    [PreserveSig]
    int MessagePending(
        IntPtr calleeTask,
        int tickCount,
        int pendingType);
}

internal sealed class OleMessageFilter : IOleMessageFilter, IDisposable
{
    private const int ServerCallIsHandled = 0;
    private const int ServerCallRejected = 1;
    private const int ServerCallRetryLater = 2;
    private const int PendingMessageWaitDefaultProcess = 2;

    private readonly OleRetryPolicy _policy;
    private IOleMessageFilter? _previous;
    private bool _registered;

    private OleMessageFilter(OleRetryPolicy policy)
    {
        _policy = policy;
    }

    public static OleMessageFilter Register(OleRetryPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
        {
            throw new InvalidOperationException(
                "OLE message filter must be registered on the Visio STA thread.");
        }

        var filter = new OleMessageFilter(policy);
        Marshal.ThrowExceptionForHR(
            CoRegisterMessageFilter(filter, out filter._previous));
        filter._registered = true;
        return filter;
    }

    internal static OleMessageFilter CreateForTest(OleRetryPolicy policy) =>
        new(policy);

    public int HandleInComingCall(
        int callType,
        IntPtr callerTask,
        int tickCount,
        IntPtr interfaceInfo) => ServerCallIsHandled;

    public int RetryRejectedCall(
        IntPtr calleeTask,
        int tickCount,
        int rejectType) => rejectType switch
        {
            ServerCallRetryLater => _policy.GetRetryDelay(
                tickCount,
                OleRejectType.RetryLater),
            ServerCallRejected => _policy.GetRetryDelay(
                tickCount,
                OleRejectType.Rejected),
            _ => -1,
        };

    public int MessagePending(
        IntPtr calleeTask,
        int tickCount,
        int pendingType) => PendingMessageWaitDefaultProcess;

    public void Dispose()
    {
        if (!_registered)
        {
            return;
        }

        Marshal.ThrowExceptionForHR(
            CoRegisterMessageFilter(_previous, out _));
        _registered = false;
        _previous = null;
    }

    [DllImport("Ole32.dll")]
    private static extern int CoRegisterMessageFilter(
        IOleMessageFilter? newFilter,
        out IOleMessageFilter? oldFilter);
}
