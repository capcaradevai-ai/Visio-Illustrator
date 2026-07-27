using VisioTestBridge.Contracts;

namespace VisioTestBridge.Core.Quality.AuditRules;

public sealed class TextOverflowRule : IFigureAuditRule
{
    public const string Category = "text_overflow";

    public IEnumerable<AuditFinding> Evaluate(FigureAuditInput input)
    {
        foreach (var item in input.Objects.Where(item => item.TextOverflows))
        {
            yield return AuditFindingFactory.Create(
                input,
                AuditSeverity.Hard,
                Category,
                [item.SemanticId],
                [AuditFindingFactory.Evidence("text_overflows", true)],
                "扩大文字框或调整可编辑文字排版，消除溢出。",
                ["文字边界完全位于对象文字框内。"],
                AuditFindingFactory.Patch(
                    input,
                    "expand_text_box",
                    [item.SemanticId]));
        }
    }
}
