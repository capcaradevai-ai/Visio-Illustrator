using VisioBridge.Contracts;

namespace VisioBridge.Core.Commands;

public interface ISequenceOperationHandler
{
    object Execute(SequenceOperation operation);
}

public sealed class SequenceExecutor
{
    private static readonly HashSet<string> AcceptedTypes = new(
        ["shape", "image", "connector", "update", "delete", "fit", "wait"],
        StringComparer.Ordinal);

    private readonly ISequenceOperationHandler _handler;

    public SequenceExecutor(ISequenceOperationHandler handler)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    public SequenceResult Execute(IReadOnlyList<SequenceOperation> operations)
    {
        ArgumentNullException.ThrowIfNull(operations);
        if (operations.Count > 500)
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.InvalidArgument,
                "sequence 最多包含 500 个操作。");
        }

        var results = new List<object>();
        for (var index = 0; index < operations.Count; index++)
        {
            var operation = operations[index];
            if (!AcceptedTypes.Contains(operation.Type))
            {
                return Failure(
                    index,
                    results,
                    new BridgeCommandException(
                        BridgeErrorCodes.UnsupportedOperation,
                        $"不支持 sequence 操作: {operation.Type}"));
            }

            try
            {
                results.Add(_handler.Execute(operation));
            }
            catch (BridgeCommandException error)
            {
                return Failure(index, results, error);
            }
        }

        return new SequenceResult(results.Count, null, results, null);
    }

    private static SequenceResult Failure(
        int index,
        IReadOnlyList<object> results,
        BridgeCommandException error) => new(
            results.Count,
            index,
            results,
            new SequenceFailure(
                error.Code,
                error.Message,
                error.Retryable,
                error.Details));
}
