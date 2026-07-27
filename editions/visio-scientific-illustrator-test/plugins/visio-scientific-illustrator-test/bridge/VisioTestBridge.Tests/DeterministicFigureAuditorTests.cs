using VisioTestBridge.Contracts;
using VisioTestBridge.Core.Quality;

namespace VisioTestBridge.Tests;

public sealed class DeterministicFigureAuditorTests
{
    [Fact]
    public void Emits_compact_stable_findings_for_deterministic_defects()
    {
        var evidence = new AuditEvidence(27, "encoder", Hash('a'), Hash('b'), Hash('c'), "adaptive-v1");
        var input = new FigureAuditInput(
            PageRevision: 27,
            RegionId: "encoder",
            Evidence: evidence,
            Objects:
            [
                new FigureAuditObject(
                    SemanticId: "node-a",
                    ExpectedText: "Encoder",
                    ActualText: "Encodr",
                    IsInsideCanvas: true,
                    TextOverflows: false,
                    MetadataValid: true,
                    IsConnector: false,
                    SourceGlued: null,
                    TargetGlued: null,
                    UnexpectedOverlapWith: [],
                    AlignmentError: 0,
                    ConnectorClearance: null,
                    RasterDeclarationValid: null),
                new FigureAuditObject(
                    SemanticId: "flow",
                    ExpectedText: null,
                    ActualText: null,
                    IsInsideCanvas: true,
                    TextOverflows: false,
                    MetadataValid: true,
                    IsConnector: true,
                    SourceGlued: true,
                    TargetGlued: false,
                    UnexpectedOverlapWith: [],
                    AlignmentError: 0,
                    ConnectorClearance: 1,
                    RasterDeclarationValid: null),
            ],
            RegionalVisualScore: 0.92,
            MinimumRegionalVisualScore: 0.90);

        var result = DeterministicFigureAuditor.Audit(input);

        Assert.Equal(2, result.HardCount);
        Assert.Equal(0, result.WarningCount);
        Assert.False(result.CanPass);
        Assert.Contains(result.Findings, finding => finding.Id == "exact_text:encoder:node-a");
        Assert.Contains(result.Findings, finding => finding.Id == "connector_glue:encoder:flow");
        Assert.All(result.Findings, finding =>
        {
            Assert.Equal(27, finding.ObservedRevision);
            Assert.NotEmpty(finding.RequiredChange);
            Assert.NotEmpty(finding.AcceptanceConditions);
        });
    }

    [Fact]
    public void Rejects_audit_input_bound_to_stale_evidence()
    {
        var evidence = new AuditEvidence(26, "encoder", Hash('a'), Hash('b'), Hash('c'), "adaptive-v1");
        var input = new FigureAuditInput(27, "encoder", evidence, [], 1, 0.9);

        var error = Assert.Throws<VisioTestBridge.Core.BridgeCommandException>(
            () => DeterministicFigureAuditor.Audit(input));

        Assert.Equal(VisioTestBridge.Core.BridgeErrorCodes.StaleAuditEvidence, error.Code);
    }

    [Fact]
    public void Evaluates_each_deterministic_rule_and_orders_findings_by_id()
    {
        var input = Input(
            [
                Object("overflow") with { TextOverflows = true },
                Object("outside") with { IsInsideCanvas = false },
                Object("metadata") with { MetadataValid = false },
                Object("raster") with { RasterDeclarationValid = false },
                Object("overlap") with { UnexpectedOverlapWith = ["outside"] },
                Object("alignment") with { AlignmentError = 0.4 },
                Object("clearance") with
                {
                    IsConnector = true,
                    SourceGlued = true,
                    TargetGlued = true,
                    ConnectorClearance = 0.1,
                },
            ],
            regionalVisualScore: 0.82,
            minimumRegionalVisualScore: 0.9,
            maximumAlignmentError: 0.25,
            minimumConnectorClearance: 0.3);

        var result = DeterministicFigureAuditor.Audit(input);

        Assert.Equal(
            [
                "alignment_spacing",
                "canvas_bounds",
                "connector_clearance",
                "raster_declaration",
                "regional_visual_metric",
                "semantic_metadata",
                "text_overflow",
                "unexpected_overlap",
            ],
            result.Findings.Select(finding => finding.Category).ToArray());
        Assert.Equal(5, result.HardCount);
        Assert.Equal(3, result.WarningCount);
        Assert.Equal(0, result.InfoCount);
        Assert.True(result.ModelReviewRequired);
        Assert.Contains("regional_visual_metric_below_threshold", result.ModelReviewReasons);
        Assert.False(result.CanPass);
    }

    [Fact]
    public void Produces_a_complete_contract_for_every_finding()
    {
        var result = DeterministicFigureAuditor.Audit(
            Input([Object("overflow") with { TextOverflows = true }]));

        var finding = Assert.Single(result.Findings);
        Assert.Equal("text_overflow:encoder:overflow", finding.Id);
        Assert.Equal(AuditSeverity.Hard, finding.Severity);
        Assert.Equal("encoder", finding.RegionId);
        Assert.Equal(["overflow"], finding.ObjectIds);
        Assert.Equal(27, finding.ObservedRevision);
        Assert.NotEmpty(finding.Evidence);
        Assert.NotEmpty(finding.RequiredChange);
        Assert.NotEmpty(finding.AcceptanceConditions);
        Assert.NotNull(finding.SafePatch);
        Assert.Equal(AuditFindingStatus.Open, finding.Status);
    }

    [Fact]
    public void Clean_current_region_can_pass_without_model_review()
    {
        var result = DeterministicFigureAuditor.Audit(Input([Object("node-a")]));

        Assert.Empty(result.Findings);
        Assert.Equal(1, result.OverallScore);
        Assert.Equal(1, result.RegionScore);
        Assert.False(result.ModelReviewRequired);
        Assert.Empty(result.ModelReviewReasons);
        Assert.True(result.CanPass);
    }

    [Fact]
    public void Finding_ids_and_order_are_stable_when_input_order_changes()
    {
        var first = DeterministicFigureAuditor.Audit(
            Input(
            [
                Object("node-b") with { MetadataValid = false },
                Object("node-a") with { TextOverflows = true },
            ]));
        var second = DeterministicFigureAuditor.Audit(
            Input(
            [
                Object("node-a") with { TextOverflows = true },
                Object("node-b") with { MetadataValid = false },
            ]));

        Assert.Equal(
            first.Findings.Select(finding => finding.Id),
            second.Findings.Select(finding => finding.Id));
    }

    [Fact]
    public void Rejects_evidence_from_another_region()
    {
        var evidence = new AuditEvidence(27, "decoder", Hash('a'), Hash('b'), Hash('c'), "adaptive-v1");
        var input = new FigureAuditInput(27, "encoder", evidence, [], 1, 0.9);

        var error = Assert.Throws<VisioTestBridge.Core.BridgeCommandException>(
            () => DeterministicFigureAuditor.Audit(input));

        Assert.Equal(VisioTestBridge.Core.BridgeErrorCodes.StaleAuditEvidence, error.Code);
    }

    private static FigureAuditInput Input(
        IReadOnlyList<FigureAuditObject> objects,
        double regionalVisualScore = 1,
        double minimumRegionalVisualScore = 0.9,
        double maximumAlignmentError = 0.25,
        double minimumConnectorClearance = 0.3)
    {
        var evidence = new AuditEvidence(27, "encoder", Hash('a'), Hash('b'), Hash('c'), "adaptive-v1");
        return new FigureAuditInput(
            27,
            "encoder",
            evidence,
            objects,
            regionalVisualScore,
            minimumRegionalVisualScore,
            maximumAlignmentError,
            minimumConnectorClearance);
    }

    private static FigureAuditObject Object(string id) =>
        new(
            SemanticId: id,
            ExpectedText: null,
            ActualText: null,
            IsInsideCanvas: true,
            TextOverflows: false,
            MetadataValid: true,
            IsConnector: false,
            SourceGlued: null,
            TargetGlued: null,
            UnexpectedOverlapWith: [],
            AlignmentError: 0,
            ConnectorClearance: null,
            RasterDeclarationValid: null);

    private static string Hash(char value) => new(value, 64);
}
