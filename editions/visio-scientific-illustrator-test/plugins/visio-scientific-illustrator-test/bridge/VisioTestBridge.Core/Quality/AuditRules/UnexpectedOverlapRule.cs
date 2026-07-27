using VisioTestBridge.Contracts;

namespace VisioTestBridge.Core.Quality.AuditRules;

public sealed class UnexpectedOverlapRule : IFigureAuditRule
{
    public const string Category = "unexpected_overlap";

    public IEnumerable<AuditFinding> Evaluate(FigureAuditInput input)
    {
        foreach (var item in input.Objects)
        {
            var overlappingIds = item.UnexpectedOverlapWith
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Append(item.SemanticId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (overlappingIds.Length <= 1)
                continue;

            yield return AuditFindingFactory.Create(
                input,
                AuditSeverity.Hard,
                Category,
                overlappingIds,
                [
                    AuditFindingFactory.Evidence(
                        "unexpected_overlap_with",
                        string.Join(",", overlappingIds.Where(id =>
                            !string.Equals(id, item.SemanticId, StringComparison.OrdinalIgnoreCase)))),
                ],
                "调整对象位置、尺寸或层级，移除非设计性的遮挡。",
                ["相关对象之间不再存在 unexpected overlap。"]);
        }
    }
}
