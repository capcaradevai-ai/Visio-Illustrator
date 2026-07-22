using VisioBridge.Core.Session;

namespace VisioBridge.Tests;

public sealed class StaCommandDispatcherTests
{
    [Fact]
    public async Task Commands_run_on_one_STA_thread_in_order()
    {
        using var dispatcher = new StaCommandDispatcher();
        var calls = new List<(int ThreadId, ApartmentState State, int Value)>();
        var first = dispatcher.InvokeAsync(() => calls.Add((
            Environment.CurrentManagedThreadId,
            Thread.CurrentThread.GetApartmentState(),
            1)));
        var second = dispatcher.InvokeAsync(() => calls.Add((
            Environment.CurrentManagedThreadId,
            Thread.CurrentThread.GetApartmentState(),
            2)));

        await Task.WhenAll(first, second);

        Assert.Equal(new[] { 1, 2 }, calls.Select(item => item.Value));
        Assert.Single(calls.Select(item => item.ThreadId).Distinct());
        Assert.All(calls, item => Assert.Equal(ApartmentState.STA, item.State));
    }

    [Fact]
    public async Task Generic_command_returns_its_value()
    {
        using var dispatcher = new StaCommandDispatcher();

        var value = await dispatcher.InvokeAsync(() => 42);

        Assert.Equal(42, value);
    }

    [Fact]
    public async Task Cancelled_command_does_not_run()
    {
        using var dispatcher = new StaCommandDispatcher();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var invoked = false;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            dispatcher.InvokeAsync(() => invoked = true, cancellation.Token));

        Assert.False(invoked);
    }
}
