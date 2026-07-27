using VisioTestBridge.Contracts;

namespace VisioTestBridge.Core.Quality.AuditRules;

public sealed class ConnectorClearanceRule : IFigureAuditRule
{
    public const string Category = "connector_clearance";

    public IEnumerable<AuditFinding> Evaluate(FigureAuditInput input)
    {
        foreach (var item in input.Objects.Where(item =>
            item.IsConnector
            && item.ConnectorClearance is double clearance
            && clearance < input.MinimumConnectorClearance))
        {
            yield return AuditFindingFactory.Create(
                input,
                AuditSeverity.Warning,
                Category,
                [item.SemanticId],
                [
                    AuditFindingFactory.Evidence("connector_clearance", item.ConnectorClearance),
                    AuditFindingFactory.Evidence(
                        "minimum_connector_clearance",
                        input.MinimumConnectorClearance),
                ],
                "调整连接线路径，使其与非端点对象保持足够净空。",
                ["connector_clearance 不小于 minimum_connector_clearance。"]);
        }
    }
}
