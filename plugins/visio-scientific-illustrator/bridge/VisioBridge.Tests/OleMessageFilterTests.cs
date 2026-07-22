using VisioBridge.Core.Interop;

namespace VisioBridge.Tests;

public sealed class OleMessageFilterTests
{
    [Theory]
    [InlineData(0, 250)]
    [InlineData(9_500, 250)]
    [InlineData(10_000, -1)]
    public void Retry_later_is_bounded(int elapsedMs, int expectedDelay)
    {
        var policy = new OleRetryPolicy(maxElapsedMs: 10_000, delayMs: 250);

        Assert.Equal(
            expectedDelay,
            policy.GetRetryDelay(elapsedMs, OleRejectType.RetryLater));
    }

    [Fact]
    public void Rejected_call_is_not_silently_retried()
    {
        var policy = new OleRetryPolicy(10_000, 250);

        Assert.Equal(-1, policy.GetRetryDelay(0, OleRejectType.Rejected));
    }

    [Theory]
    [InlineData(2, 250)]
    [InlineData(1, -1)]
    [InlineData(99, -1)]
    public void Filter_delegates_known_reject_types(int rejectType, int expectedDelay)
    {
        var filter = OleMessageFilter.CreateForTest(new OleRetryPolicy(10_000, 250));

        Assert.Equal(
            expectedDelay,
            filter.RetryRejectedCall(IntPtr.Zero, 0, rejectType));
    }

    [Fact]
    public void Registration_is_rejected_outside_an_STA_thread()
    {
        Exception? captured = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var _ = OleMessageFilter.Register(new OleRetryPolicy(10_000, 250));
            }
            catch (Exception error)
            {
                captured = error;
            }
        });
        thread.SetApartmentState(ApartmentState.MTA);
        thread.Start();
        thread.Join();

        Assert.IsType<InvalidOperationException>(captured);
    }
}
