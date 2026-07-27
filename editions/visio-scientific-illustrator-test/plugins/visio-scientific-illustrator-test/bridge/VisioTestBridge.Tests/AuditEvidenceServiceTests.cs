using System.Text;
using VisioTestBridge.Core;
using VisioTestBridge.Core.Quality;

namespace VisioTestBridge.Tests;

public sealed class AuditEvidenceServiceTests
{
    [Fact]
    public void Creates_preview_hash_from_fresh_exported_bytes()
    {
        var preview = new MemoryPreviewSource(Encoding.UTF8.GetBytes("fresh-region-preview"));
        var service = new AuditEvidenceService(preview);

        var evidence = service.Create(
            pageRevision: 27,
            regionId: "encoder",
            referenceCropSha256: Hash('a'),
            structureSnapshotSha256: Hash('b'),
            auditProfile: "adaptive-v1");

        Assert.Equal(1, preview.ExportCount);
        Assert.Equal(
            "9775b0c3c0864893cac5bbfcc201faf5ec2e4d455565a63b4306c69027fbb342",
            evidence.PreviewSha256);
    }

    [Fact]
    public void Rejects_evidence_when_revision_or_structure_has_changed()
    {
        var service = new AuditEvidenceService(new MemoryPreviewSource([1, 2, 3]));
        var evidence = service.Create(27, "encoder", Hash('a'), Hash('b'), "adaptive-v1");

        var error = Assert.Throws<BridgeCommandException>(() =>
            service.RequireCurrent(evidence, currentRevision: 28, currentStructureSha256: Hash('c')));

        Assert.Equal(BridgeErrorCodes.StaleAuditEvidence, error.Code);
        Assert.True(error.Retryable);
        Assert.NotNull(error.Details);
    }

    private static string Hash(char value) => new(value, 64);

    private sealed class MemoryPreviewSource(byte[] bytes) : IAuditPreviewSource
    {
        public int ExportCount { get; private set; }

        public byte[] ExportCurrentRegion(string regionId)
        {
            ExportCount++;
            return bytes;
        }
    }
}
