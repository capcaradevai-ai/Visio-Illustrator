using VisioTestBridge.Core.Session;

namespace VisioTestBridge.IntegrationTests;

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

    [Trait("Category", "VisioIntegration")]
    [Fact]
    public void Dispose_closes_the_owned_Visio_process_without_explicit_shutdown()
    {
        var session = new VisioSession();
        var result = session.LaunchNew("Figure", 800, 500, 96, false);

        session.Dispose();

        Assert.True(WaitForOwnedProcessExit(result.ProcessId, TimeSpan.FromSeconds(35)));
    }

    [Trait("Category", "VisioIntegration")]
    [Fact]
    public void Open_activates_requested_page_and_reads_its_real_canvas()
    {
        var directory = CreateTestDirectory();
        var path = Path.Combine(directory, "multipage.vsdx");
        try
        {
            using (var creator = new VisioSession())
            {
                creator.LaunchNew("Overview", 800, 500, 100, false);
                creator.AddPageForIntegrationTest(
                    "Results",
                    1200,
                    600,
                    100,
                    activate: false);
                creator.Save(new VisioTestBridge.Contracts.SaveCommand(path));
                creator.Shutdown(true, "discard");
            }

            using var session = new VisioSession();
            var result = session.Open(path, "Results", 100, false);
            try
            {
                Assert.Equal("Results", result.PageName);
                Assert.Equal(1200, result.Canvas.WidthPx);
                Assert.Equal(600, result.Canvas.HeightPx);
                Assert.Equal(
                    ("Results", "Results"),
                    session.ReadPageBindingForIntegrationTest());
            }
            finally
            {
                session.Shutdown(true, "discard");
            }
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Trait("Category", "VisioIntegration")]
    [Fact]
    public void Operations_and_safe_save_keep_the_original_bound_page_visible()
    {
        var directory = CreateTestDirectory();
        var path = Path.Combine(directory, "bound-page.vsdx");
        try
        {
            using var fixture = VisioFixture.StartVisible();
            fixture.AddShape(new VisioTestBridge.Contracts.AddShapeCommand(
                "bound-shape",
                "rectangle",
                new VisioTestBridge.Contracts.PixelBox(100, 100, 200, 120),
                "Bound"));
            fixture.AddPage("Other", 1600, 1000, 96, activate: true);
            Assert.Equal(("Figure", "Other"), fixture.ReadPageBinding());

            fixture.Fit("page");
            Assert.Equal(("Figure", "Figure"), fixture.ReadPageBinding());

            fixture.AddPage("OtherBeforeSave", 1600, 1000, 96, activate: true);
            fixture.Save(path);

            Assert.Equal(("Figure", "Figure"), fixture.ReadPageBinding());
            Assert.Contains(
                fixture.Inspect().Shapes,
                shape => shape.SemanticId == "bound-shape");
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Trait("Category", "VisioIntegration")]
    [Fact]
    public void External_Visio_exit_transitions_the_session_to_invalidated()
    {
        using var fixture = VisioFixture.StartVisible();
        fixture.QuitApplicationExternally();

        var error = Assert.Throws<VisioTestBridge.Core.BridgeCommandException>(
            () => fixture.Inspect());

        Assert.Equal(VisioTestBridge.Core.BridgeErrorCodes.SessionInvalidated, error.Code);
        Assert.Equal(SessionState.Invalidated, fixture.State);
        Assert.Equal(SessionState.Invalidated, fixture.GetStatus().State);
    }

    private static string CreateTestDirectory()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "visio-session-integration",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
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
