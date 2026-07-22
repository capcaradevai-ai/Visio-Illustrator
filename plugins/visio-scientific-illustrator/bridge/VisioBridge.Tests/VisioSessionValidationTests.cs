using VisioBridge.Core;
using VisioBridge.Core.Session;

namespace VisioBridge.Tests;

public sealed class VisioSessionValidationTests
{
    [Fact]
    public void LaunchNew_rejects_invalid_canvas_without_starting_Visio()
    {
        using var session = new VisioSession();

        var error = Assert.Throws<BridgeCommandException>(() =>
            session.LaunchNew("Figure", 0, 1000, 96, true));

        Assert.Equal(BridgeErrorCodes.InvalidArgument, error.Code);
        Assert.Equal(SessionState.Stopped, session.State);
        Assert.Null(session.OwnedProcessId);
    }

    [Fact]
    public void Open_rejects_relative_paths_without_starting_Visio()
    {
        using var session = new VisioSession();

        var error = Assert.Throws<BridgeCommandException>(() =>
            session.Open("figure.vsdx", 1600, 1000, 96, true));

        Assert.Equal(BridgeErrorCodes.InvalidArgument, error.Code);
        Assert.Equal(SessionState.Stopped, session.State);
        Assert.Null(session.OwnedProcessId);
    }
}
