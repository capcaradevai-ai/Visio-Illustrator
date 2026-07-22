using VisioBridge.Core.Commands;
using VisioBridge.Core.Session;

namespace VisioBridge.IntegrationTests;

public sealed class VisioFixture : IDisposable
{
    private readonly VisioSession _session;
    private bool _disposed;

    private VisioFixture(VisioSession session)
    {
        _session = session;
        Router = new CommandRouter(session);
    }

    public CommandRouter Router { get; }

    public int OwnedProcessId => _session.OwnedProcessId
        ?? throw new InvalidOperationException("Fixture 没有 owned Visio PID。");

    public static VisioFixture StartVisible()
    {
        var session = new VisioSession();
        try
        {
            session.LaunchNew("Figure", 1600, 1000, 96, true);
            return new VisioFixture(session);
        }
        catch
        {
            session.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_session.OwnedProcessId is not null)
        {
            _session.Shutdown(true, "discard");
        }
        _session.Dispose();
    }
}
