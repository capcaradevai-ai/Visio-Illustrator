using VisioBridge.Core.Commands;
using VisioBridge.Tests.Fakes;

namespace VisioBridge.Tests;

public sealed class SequenceExecutorTests
{
    [Fact]
    public void Sequence_stops_at_first_failure_and_reports_applied_count()
    {
        var fixture = SequenceFixture.WithResults(true, false, true);

        var result = fixture.Executor.Execute(SequenceFixture.ThreeOperations());

        Assert.Equal(1, result.AppliedCount);
        Assert.Equal(1, result.FailedIndex);
        Assert.Equal(2, fixture.Calls.Count);
    }
}
