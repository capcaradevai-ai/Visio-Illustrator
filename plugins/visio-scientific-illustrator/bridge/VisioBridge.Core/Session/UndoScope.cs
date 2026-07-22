using Visio = Microsoft.Office.Interop.Visio;

namespace VisioBridge.Core.Session;

internal sealed class UndoScope : IDisposable
{
    private readonly Visio.InvisibleApp _application;
    private readonly int _scopeId;
    private bool _commit;
    private bool _disposed;

    public UndoScope(Visio.InvisibleApp application, string name)
    {
        _application = application;
        _scopeId = application.BeginUndoScope(name);
    }

    public void Complete() => _commit = true;

    public void Dispose()
    {
        if (_disposed) return;
        _application.EndUndoScope(_scopeId, _commit);
        _disposed = true;
    }
}
