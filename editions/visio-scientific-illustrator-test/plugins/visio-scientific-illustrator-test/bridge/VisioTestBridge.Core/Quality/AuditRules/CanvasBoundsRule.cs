using VisioTestBridge.Contracts;

namespace VisioTestBridge.Core.Quality.AuditRules;

public sealed class CanvasBoundsRule : IFigureAuditRule
{
    public const string Category = "canvas_bounds";

    public IEnumerable<AuditFinding> Evaluate(FigureAuditInput input)
    {
        foreach (var item in input.Objects.Where(item => !item.IsInsideCanvas))
        {
            yield return AuditFindingFactory.Create(
                input,
                AuditSeverity.Hard,
                Category,
                [item.SemanticId],
                [AuditFindingFactory.Evidence("is_inside_canvas", false)],
                "将对象完整移回页面可见边界内。",
                ["对象边界不越过页面边界。"],
                AuditFindingFactory.Patch(
                    input,
                    "move_inside_canvas",
                    [item.SemanticId]));
        }
    }
}
