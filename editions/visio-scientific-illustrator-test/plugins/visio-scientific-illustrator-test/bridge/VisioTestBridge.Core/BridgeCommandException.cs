namespace VisioTestBridge.Core;

public sealed class BridgeCommandException : Exception
{
    public BridgeCommandException(
        string code,
        string message,
        bool retryable = false,
        object? details = null)
        : base(message)
    {
        Code = code;
        Retryable = retryable;
        Details = details;
    }

    public string Code { get; }

    public bool Retryable { get; }

    public object? Details { get; }

    public static BridgeCommandException Unsupported(string message) =>
        new(BridgeErrorCodes.UnsupportedOperation, message);
}
