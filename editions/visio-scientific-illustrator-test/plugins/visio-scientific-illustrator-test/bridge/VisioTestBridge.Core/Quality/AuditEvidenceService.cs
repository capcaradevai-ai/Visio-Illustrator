using System.Security.Cryptography;
using VisioTestBridge.Contracts;

namespace VisioTestBridge.Core.Quality;

public interface IAuditPreviewSource
{
    byte[] ExportCurrentRegion(string regionId);
}

public sealed class AuditEvidenceService(IAuditPreviewSource previewSource)
{
    public AuditEvidence Create(
        long pageRevision,
        string regionId,
        string referenceCropSha256,
        string structureSnapshotSha256,
        string auditProfile)
    {
        if (pageRevision < 0
            || string.IsNullOrWhiteSpace(regionId)
            || string.IsNullOrWhiteSpace(auditProfile))
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.InvalidArgument,
                "审计证据必须包含非负修订号、区域和质量配置。");
        }
        ValidateHash(referenceCropSha256, nameof(referenceCropSha256));
        ValidateHash(structureSnapshotSha256, nameof(structureSnapshotSha256));

        var previewBytes = previewSource.ExportCurrentRegion(regionId);
        if (previewBytes is null || previewBytes.Length == 0)
            throw new BridgeCommandException(
                BridgeErrorCodes.InvalidArgument,
                "当前区域预览为空，不能创建审计证据。");

        return new AuditEvidence(
            pageRevision,
            regionId,
            Convert.ToHexString(SHA256.HashData(previewBytes)).ToLowerInvariant(),
            referenceCropSha256,
            structureSnapshotSha256,
            auditProfile);
    }

    public void RequireCurrent(
        AuditEvidence evidence,
        long currentRevision,
        string currentStructureSha256)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        ValidateHash(currentStructureSha256, nameof(currentStructureSha256));
        if (evidence.PageRevision == currentRevision
            && string.Equals(
                evidence.StructureSnapshotSha256,
                currentStructureSha256,
                StringComparison.Ordinal))
        {
            return;
        }

        throw new BridgeCommandException(
            BridgeErrorCodes.StaleAuditEvidence,
            "审计证据与当前页面修订或结构不一致。",
            retryable: true,
            details: new
            {
                observed_revision = evidence.PageRevision,
                current_revision = currentRevision,
                observed_structure_sha256 = evidence.StructureSnapshotSha256,
                current_structure_sha256 = currentStructureSha256,
            });
    }

    private static void ValidateHash(string value, string name)
    {
        if (value is null
            || value.Length != 64
            || value.Any(character =>
                character is not (>= '0' and <= '9')
                and not (>= 'a' and <= 'f')))
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.InvalidArgument,
                $"{name} 必须是 64 位小写 SHA-256。");
        }
    }
}
