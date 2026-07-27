using VisioTestBridge.Contracts;

namespace VisioTestBridge.Core.Quality.AuditRules;

public sealed class SemanticMetadataRule : IFigureAuditRule
{
    public const string Category = "semantic_metadata";

    public IEnumerable<AuditFinding> Evaluate(FigureAuditInput input)
    {
        foreach (var item in input.Objects.Where(item => !item.MetadataValid))
        {
            yield return AuditFindingFactory.Create(
                input,
                AuditSeverity.Hard,
                Category,
                [item.SemanticId],
                [AuditFindingFactory.Evidence("metadata_valid", false)],
                "补齐或修正 Test Edition 要求的对象语义元数据。",
                ["对象包含合法且一致的 semantic ID、region、role 与 revision 元数据。"]);
        }
    }
}
