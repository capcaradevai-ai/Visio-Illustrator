using VisioTestBridge.Contracts;

namespace VisioTestBridge.Core.Quality.AuditRules;

public sealed class AlignmentSpacingRule : IFigureAuditRule
{
    public const string Category = "alignment_spacing";

    public IEnumerable<AuditFinding> Evaluate(FigureAuditInput input)
    {
        foreach (var item in input.Objects.Where(item =>
            Math.Abs(item.AlignmentError) > input.MaximumAlignmentError))
        {
            yield return AuditFindingFactory.Create(
                input,
                AuditSeverity.Warning,
                Category,
                [item.SemanticId],
                [
                    AuditFindingFactory.Evidence("alignment_error", item.AlignmentError),
                    AuditFindingFactory.Evidence("maximum_alignment_error", input.MaximumAlignmentError),
                ],
                "按区域基线或分布节奏重新对齐对象。",
                ["alignment_error 不大于 maximum_alignment_error。"]);
        }
    }
}
