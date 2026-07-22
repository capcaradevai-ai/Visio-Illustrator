using VisioBridge.Core.Session;

namespace VisioBridge.IntegrationTests;

public sealed class SessionIntegrationTests
{
    [Trait("Category", "VisioIntegration")]
    [Fact]
    public void LaunchNew_creates_visible_owned_session()
    {
        var ownedProcessId = 0;
        using (var session = new VisioSession())
        {
            var result = session.LaunchNew("Figure", 1600, 1000, 96, true);
            try
            {
                Assert.True(result.Visible);
                Assert.True(result.ProcessId > 0);
                using (var ownedProcess = System.Diagnostics.Process.GetProcessById(result.ProcessId))
                {
                    Assert.Equal("VISIO", ownedProcess.ProcessName, ignoreCase: true);
                }
                Assert.Equal(SessionState.Ready, session.State);
                Assert.Equal(result.ProcessId, session.OwnedProcessId);
                ownedProcessId = result.ProcessId;
            }
            finally
            {
                session.Shutdown(true, "discard");
            }
        }

        Assert.True(WaitForOwnedProcessExit(ownedProcessId, TimeSpan.FromSeconds(35)));
    }

    private static bool WaitForOwnedProcessExit(int processId, TimeSpan timeout)
    {
        try
        {
            using var process = System.Diagnostics.Process.GetProcessById(processId);
            return process.WaitForExit((int)timeout.TotalMilliseconds);
        }
        catch (ArgumentException)
        {
            return true;
        }
    }
}
