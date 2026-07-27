using VisioTestBridge.Contracts;

namespace VisioTestBridge.Core.Quality.AuditRules;

public sealed class EvidenceFreshnessRule : IFigureAuditRule
{
    public IEnumerable<AuditFinding> Evaluate(FigureAuditInput input)
    {
        ValidateHash(input.Evidence.PreviewSha256, "preview_sha256");
        ValidateHash(input.Evidence.ReferenceCropSha256, "reference_crop_sha256");
        ValidateHash(input.Evidence.StructureSnapshotSha256, "structure_snapshot_sha256");
        if (string.IsNullOrWhiteSpace(input.Evidence.AuditProfile))
            throw InvalidEvidence("audit_profile 不能为空。");

        if (input.Evidence.PageRevision != input.PageRevision
            || !string.Equals(
                input.Evidence.RegionId,
                input.RegionId,
                StringComparison.Ordinal))
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.StaleAuditEvidence,
                "审计证据与当前修订或区域不一致。",
                retryable: true,
                details: new
                {
                    observed_revision = input.Evidence.PageRevision,
                    current_revision = input.PageRevision,
                    observed_region_id = input.Evidence.RegionId,
                    current_region_id = input.RegionId,
                });
        }

        return [];
    }

    private static void ValidateHash(string value, string field)
    {
        if (value is null
            || value.Length != 64
            || value.Any(character => character is not (>= '0' and <= '9')
                and not (>= 'a' and <= 'f')))
        {
            throw InvalidEvidence($"{field} 必须是 64 位小写 SHA-256。");
        }
    }

    private static BridgeCommandException InvalidEvidence(string message) =>
        new(BridgeErrorCodes.InvalidArgument, message);
}
