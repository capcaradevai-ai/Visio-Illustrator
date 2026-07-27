using VisioTestBridge.Contracts;

namespace VisioTestBridge.Core.Quality.AuditRules;

public sealed class ExactTextRule : IFigureAuditRule
{
    public const string Category = "exact_text";

    public IEnumerable<AuditFinding> Evaluate(FigureAuditInput input)
    {
        foreach (var item in input.Objects)
        {
            if (item.ExpectedText is null
                || string.Equals(item.ExpectedText, item.ActualText, StringComparison.Ordinal))
            {
                continue;
            }

            yield return AuditFindingFactory.Create(
                input,
                AuditSeverity.Hard,
                Category,
                [item.SemanticId],
                [
                    AuditFindingFactory.Evidence("expected_text", item.ExpectedText),
                    AuditFindingFactory.Evidence("actual_text", item.ActualText),
                ],
                "将文字修正为参考图中的精确内容。",
                ["对象文字与 expected_text 完全一致。"]);
        }
    }
}
