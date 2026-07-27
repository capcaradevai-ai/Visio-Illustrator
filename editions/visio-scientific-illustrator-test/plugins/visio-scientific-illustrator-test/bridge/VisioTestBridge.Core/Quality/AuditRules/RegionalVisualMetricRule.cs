using VisioTestBridge.Contracts;

namespace VisioTestBridge.Core.Quality.AuditRules;

public sealed class RegionalVisualMetricRule : IFigureAuditRule
{
    public const string Category = "regional_visual_metric";

    public IEnumerable<AuditFinding> Evaluate(FigureAuditInput input)
    {
        if (input.RegionalVisualScore >= input.MinimumRegionalVisualScore)
            return [];

        return
        [
            AuditFindingFactory.Create(
                input,
                AuditSeverity.Warning,
                Category,
                [input.RegionId],
                [
                    AuditFindingFactory.Evidence(
                        "regional_visual_score",
                        input.RegionalVisualScore),
                    AuditFindingFactory.Evidence(
                        "minimum_regional_visual_score",
                        input.MinimumRegionalVisualScore),
                ],
                "人工复核区域预览，并按参考裁剪修正主要视觉差异。",
                ["区域视觉分数达到阈值，或审查者记录有证据的 waiver。"]),
        ];
    }
}
