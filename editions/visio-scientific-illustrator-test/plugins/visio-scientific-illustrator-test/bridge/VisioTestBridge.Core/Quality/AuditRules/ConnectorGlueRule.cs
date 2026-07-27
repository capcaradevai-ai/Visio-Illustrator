using VisioTestBridge.Contracts;

namespace VisioTestBridge.Core.Quality.AuditRules;

public sealed class ConnectorGlueRule : IFigureAuditRule
{
    public const string Category = "connector_glue";

    public IEnumerable<AuditFinding> Evaluate(FigureAuditInput input)
    {
        foreach (var item in input.Objects.Where(item =>
            item.IsConnector && (item.SourceGlued != true || item.TargetGlued != true)))
        {
            yield return AuditFindingFactory.Create(
                input,
                AuditSeverity.Hard,
                Category,
                [item.SemanticId],
                [
                    AuditFindingFactory.Evidence("source_glued", item.SourceGlued),
                    AuditFindingFactory.Evidence("target_glued", item.TargetGlued),
                ],
                "将连接线两个端点真实粘附到目标连接点。",
                ["source_glued 与 target_glued 均为 true。"]);
        }
    }
}
