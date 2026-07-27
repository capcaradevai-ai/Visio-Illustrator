using VisioTestBridge.Contracts;

namespace VisioTestBridge.Core.Quality.AuditRules;

public sealed class RasterDeclarationRule : IFigureAuditRule
{
    public const string Category = "raster_declaration";

    public IEnumerable<AuditFinding> Evaluate(FigureAuditInput input)
    {
        foreach (var item in input.Objects.Where(item =>
            item.RasterDeclarationValid == false))
        {
            yield return AuditFindingFactory.Create(
                input,
                AuditSeverity.Hard,
                Category,
                [item.SemanticId],
                [AuditFindingFactory.Evidence("raster_declaration_valid", false)],
                "补齐并校正不可约栅格单元声明，或将可重建内容拆为 Visio 图元。",
                ["栅格声明完整、可持久审计，且 contains_reconstructable_content=false。"]);
        }
    }
}
