using VisioTestBridge.Contracts;
using VisioTestBridge.Core;
using VisioTestBridge.Core.Quality;
using VisioTestBridge.Core.Session;

namespace VisioTestBridge.IntegrationTests;

public sealed class AuditPersistenceIntegrationTests
{
    [Trait("Category", "VisioIntegration")]
    [Fact]
    public void Audit_uses_current_Visio_text_instead_of_caller_claims()
    {
        using var fixture = VisioFixture.StartVisible();
        fixture.AddShape(new AddShapeCommand(
            "observed-text",
            "rectangle",
            new PixelBox(200, 200, 300, 160),
            "Actual"));
        var quality = fixture.GetQualityStatus();
        var structure = fixture.GetStructureHash();
        var claimed = new FigureAuditObject(
            "observed-text",
            "Expected",
            "Expected",
            true,
            false,
            true,
            false,
            null,
            null,
            [],
            0,
            null,
            null);

        var result = fixture.AuditFigure(new FigureAuditInput(
            quality.PageRevision,
            "main",
            new AuditEvidence(
                quality.PageRevision,
                "main",
                new string('a', 64),
                new string('b', 64),
                structure.StructureSnapshotSha256,
                "strict"),
            [claimed],
            1,
            0.9));

        Assert.Contains(
            result.Findings,
            finding => finding.Category == "exact_text"
                && finding.ObjectIds.Contains("observed-text"));
    }

    [Trait("Category", "VisioIntegration")]
    [Fact]
    public void Waiving_the_only_current_warning_allows_the_region_gate()
    {
        using var fixture = VisioFixture.StartVisible();
        var quality = fixture.GetQualityStatus();
        var structure = fixture.GetStructureHash();
        var result = fixture.AuditFigure(new FigureAuditInput(
            quality.PageRevision,
            "main",
            new AuditEvidence(
                quality.PageRevision,
                "main",
                new string('a', 64),
                new string('b', 64),
                structure.StructureSnapshotSha256,
                "strict"),
            [],
            0.8,
            0.9));
        var finding = Assert.Single(result.Findings);

        fixture.SetAuditDecision(new SetAuditDecisionCommand(
            finding.Id,
            "waive",
            "人工复核确认该视觉差异不影响科学含义。",
            quality.PageRevision));
        var gate = fixture.AssertQualityGate("main");

        Assert.True(gate.Passed);
        Assert.Equal(RegionQualityState.Passed, fixture.GetQualityStatus().State);
    }

    [Trait("Category", "VisioIntegration")]
    [Fact]
    public void Audit_history_survives_save_and_reopen_without_reusing_old_pass()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "visio-audit-persistence-integration",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var target = Path.Combine(directory, "audit-history.vsdx");
        try
        {
            using (var fixture = VisioFixture.StartVisible())
            {
                fixture.AddShape(new AddShapeCommand(
                    "audit-shape",
                    "rectangle",
                    new PixelBox(200, 200, 300, 160),
                    "Audit"));
                var quality = fixture.GetQualityStatus();
                var structure = fixture.GetStructureHash();
                var hash = new string('a', 64);
                var result = fixture.AuditFigure(new FigureAuditInput(
                    quality.PageRevision,
                    "main",
                    new AuditEvidence(
                        quality.PageRevision,
                        "main",
                        hash,
                        new string('b', 64),
                        structure.StructureSnapshotSha256,
                        "strict"),
                    [],
                    1,
                    0.9));

                Assert.True(result.CanPass);
                var history = fixture.ReadAuditHistory();
                Assert.Equal(["main"], history.RegionIds);
                Assert.Single(history.RegionAudits);
                Assert.Empty(history.WholePageAudits);
                fixture.Save(target);
            }

            using var reopened = new VisioSession();
            reopened.Open(target, "Figure", 96, false);
            try
            {
                var history = reopened.ReadAuditHistoryForIntegrationTest();
                var quality = reopened.GetQualityStatus();

                Assert.Equal(["main"], history.RegionIds);
                Assert.Single(history.RegionAudits);
                Assert.Equal("strict", history.RegionAudits[0].Profile);
                Assert.Equal(RegionQualityState.AwaitingAudit, quality.State);
                Assert.Null(quality.LastPassedRevision);
                Assert.NotEqual(
                    new string('0', 64),
                    reopened.GetStructureHash().StructureSnapshotSha256);
            }
            finally
            {
                reopened.Shutdown(true, "discard");
            }
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Trait("Category", "VisioIntegration")]
    [Fact]
    public void External_visible_edit_invalidates_the_old_pass_and_revision()
    {
        using var fixture = VisioFixture.StartVisible();
        fixture.AddShape(new AddShapeCommand(
            "external-edit",
            "rectangle",
            new PixelBox(200, 200, 300, 160),
            "Before"));
        var quality = fixture.GetQualityStatus();
        var structure = fixture.GetStructureHash();
        var result = fixture.AuditFigure(new FigureAuditInput(
            quality.PageRevision,
            "main",
            new AuditEvidence(
                quality.PageRevision,
                "main",
                new string('a', 64),
                new string('b', 64),
                structure.StructureSnapshotSha256,
                "strict"),
            [],
            1,
            0.9));
        Assert.True(result.CanPass);

        fixture.EditShapeTextExternally("external-edit", "After");
        var invalidated = fixture.GetQualityStatus();

        Assert.Equal(quality.PageRevision + 1, invalidated.PageRevision);
        Assert.Equal(RegionQualityState.AwaitingAudit, invalidated.State);
        Assert.Null(invalidated.LastPassedRevision);
        var error = Assert.Throws<BridgeCommandException>(
            () => fixture.AssertQualityGate("main"));
        Assert.Equal(BridgeErrorCodes.QualityGateBlocked, error.Code);
    }
}
