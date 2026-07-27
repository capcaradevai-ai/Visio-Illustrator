using VisioTestBridge.Contracts;
using VisioTestBridge.Core.Quality.AuditRules;

namespace VisioTestBridge.Core.Quality;

public interface IFigureAuditRule
{
    IEnumerable<AuditFinding> Evaluate(FigureAuditInput input);
}

public static class DeterministicFigureAuditor
{
    private static readonly IFigureAuditRule[] Rules =
    [
        new EvidenceFreshnessRule(),
        new ExactTextRule(),
        new TextOverflowRule(),
        new CanvasBoundsRule(),
        new UnexpectedOverlapRule(),
        new AlignmentSpacingRule(),
        new ConnectorGlueRule(),
        new ConnectorClearanceRule(),
        new SemanticMetadataRule(),
        new RasterDeclarationRule(),
        new RegionalVisualMetricRule(),
    ];

    public static FigureAuditResult Audit(FigureAuditInput input)
    {
        ValidateInput(input);

        var findings = Rules
            .SelectMany(rule => rule.Evaluate(input))
            .GroupBy(finding => finding.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(finding => finding.Id, StringComparer.Ordinal)
            .ToArray();

        var hardCount = findings.Count(finding => finding.Severity == AuditSeverity.Hard);
        var warningCount = findings.Count(finding => finding.Severity == AuditSeverity.Warning);
        var infoCount = findings.Count(finding => finding.Severity == AuditSeverity.Info);
        var modelReviewReasons = findings
            .Where(finding => finding.Category == RegionalVisualMetricRule.Category)
            .Select(_ => "regional_visual_metric_below_threshold")
            .Distinct(StringComparer.Ordinal)
            .OrderBy(reason => reason, StringComparer.Ordinal)
            .ToArray();
        var regionScore = Math.Clamp(
            input.RegionalVisualScore - (hardCount * 0.1) - (warningCount * 0.03) - (infoCount * 0.01),
            0,
            1);

        return new FigureAuditResult(
            OverallScore: regionScore,
            RegionScore: regionScore,
            HardCount: hardCount,
            WarningCount: warningCount,
            InfoCount: infoCount,
            Findings: findings,
            ModelReviewRequired: modelReviewReasons.Length > 0,
            ModelReviewReasons: modelReviewReasons,
            CanPass: hardCount == 0 && warningCount == 0,
            Evidence: input.Evidence);
    }

    private static void ValidateInput(FigureAuditInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.Evidence);
        ArgumentNullException.ThrowIfNull(input.Objects);
        if (input.PageRevision < 0
            || string.IsNullOrWhiteSpace(input.RegionId)
            || !double.IsFinite(input.RegionalVisualScore)
            || input.RegionalVisualScore is < 0 or > 1
            || !double.IsFinite(input.MinimumRegionalVisualScore)
            || input.MinimumRegionalVisualScore is < 0 or > 1
            || !double.IsFinite(input.MaximumAlignmentError)
            || input.MaximumAlignmentError < 0
            || !double.IsFinite(input.MinimumConnectorClearance)
            || input.MinimumConnectorClearance < 0)
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.InvalidArgument,
                "审计输入包含无效的修订号、区域或阈值。");
        }

        foreach (var item in input.Objects)
        {
            if (item is null || string.IsNullOrWhiteSpace(item.SemanticId))
                throw new BridgeCommandException(
                    BridgeErrorCodes.InvalidArgument,
                    "每个审计对象都必须包含 semantic_id。");
            if (item.UnexpectedOverlapWith is null
                || !double.IsFinite(item.AlignmentError)
                || item.ConnectorClearance is double clearance && !double.IsFinite(clearance))
            {
                throw new BridgeCommandException(
                    BridgeErrorCodes.InvalidArgument,
                    $"审计对象 {item.SemanticId} 包含无效的规则输入。");
            }
        }
    }
}

internal static class AuditFindingFactory
{
    public static AuditFinding Create(
        FigureAuditInput input,
        AuditSeverity severity,
        string category,
        IEnumerable<string> objectIds,
        IEnumerable<KeyValuePair<string, string>> evidence,
        string requiredChange,
        IEnumerable<string> acceptanceConditions,
        AuditSafePatch? safePatch = null)
    {
        var ids = objectIds
            .Select(objectId => objectId.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(objectId => objectId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var orderedEvidence = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in evidence)
            orderedEvidence[pair.Key] = pair.Value;

        return new AuditFinding(
            Id: FindingId.Create(category, input.RegionId, ids),
            Severity: severity,
            Category: category,
            RegionId: input.RegionId,
            ObjectIds: ids,
            ObservedRevision: input.PageRevision,
            Evidence: orderedEvidence,
            RequiredChange: requiredChange,
            AcceptanceConditions: acceptanceConditions.ToArray(),
            SafePatch: safePatch,
            Status: AuditFindingStatus.Open);
    }

    public static AuditSafePatch Patch(
        FigureAuditInput input,
        string operation,
        IEnumerable<string> objectIds,
        params (string Key, string Value)[] arguments) =>
        new(
            operation,
            objectIds.ToArray(),
            input.PageRevision,
            arguments.ToDictionary(
                argument => argument.Key,
                argument => argument.Value,
                StringComparer.Ordinal));

    public static KeyValuePair<string, string> Evidence(string key, object? value) =>
        new(key, Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty);
}
