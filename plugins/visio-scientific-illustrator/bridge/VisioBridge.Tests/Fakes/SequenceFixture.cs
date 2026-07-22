using VisioBridge.Contracts;
using VisioBridge.Core.Commands;

namespace VisioBridge.Tests.Fakes;

internal sealed class SequenceFixture : ISequenceOperationHandler
{
    private readonly Queue<bool> _results;

    private SequenceFixture(IEnumerable<bool> results)
    {
        _results = new Queue<bool>(results);
        Executor = new SequenceExecutor(this);
    }

    public SequenceExecutor Executor { get; }
    public List<string> Calls { get; } = [];

    public static SequenceFixture WithResults(params bool[] results) => new(results);

    public static IReadOnlyList<SequenceOperation> ThreeOperations() =>
    [
        new("wait"),
        new("update"),
        new("fit"),
    ];

    public object Execute(SequenceOperation operation)
    {
        Calls.Add(operation.Type);
        if (!_results.Dequeue())
        {
            throw new VisioBridge.Core.BridgeCommandException(
                VisioBridge.Core.BridgeErrorCodes.InvalidArgument,
                "injected failure");
        }
        return new { operation.Type };
    }
}
