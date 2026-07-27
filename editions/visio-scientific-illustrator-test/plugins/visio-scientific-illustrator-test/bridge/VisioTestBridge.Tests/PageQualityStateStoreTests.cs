using VisioTestBridge.Contracts;
using VisioTestBridge.Core;
using VisioTestBridge.Core.Persistence;

namespace VisioTestBridge.Tests;

public sealed class PageQualityStateStoreTests
{
    [Fact]
    public void Writes_and_reads_the_complete_page_quality_contract()
    {
        var cells = new MemoryQualityCellStore();
        var store = new PageQualityStateStore(cells);
        var expected = new PageQualityState(
            ProtocolVersion: "0.4.0-test.1",
            PageRevision: 27,
            CurrentRegion: "encoder",
            QualityState: RegionQualityState.AwaitingAudit,
            LastPassedRevision: 21,
            PreviewSha256: Hash('a'),
            StructureSha256: Hash('b'),
            SolutionXmlSha256: Hash('c'));

        store.Write(expected);

        Assert.Equal(expected, store.Read());
        Assert.Equal(PageQualityStateStore.RequiredCells.Order(), cells.Values.Keys.Order());
    }

    [Fact]
    public void Rejects_missing_or_malformed_persisted_state()
    {
        var cells = new MemoryQualityCellStore();
        var store = new PageQualityStateStore(cells);

        var missing = Assert.Throws<BridgeCommandException>(() => store.Read());
        Assert.Equal(BridgeErrorCodes.AuditStateCorrupt, missing.Code);

        cells.Values["User.VsiProtocolVersion"] = "0.4.0-test.1";
        cells.Values["User.VsiPageRevision"] = "-1";
        cells.Values["User.VsiCurrentRegion"] = "encoder";
        cells.Values["User.VsiQualityState"] = "awaiting_audit";
        cells.Values["User.VsiLastPassedRevision"] = "";
        cells.Values["User.VsiPreviewSha256"] = Hash('a');
        cells.Values["User.VsiStructureSha256"] = Hash('b');
        cells.Values["User.VsiSolutionXmlSha256"] = Hash('c');

        var malformed = Assert.Throws<BridgeCommandException>(() => store.Read());
        Assert.Equal(BridgeErrorCodes.AuditStateCorrupt, malformed.Code);
    }

    [Fact]
    public void Writes_complete_object_metadata_without_omitting_revision_identity()
    {
        var cells = new MemoryQualityCellStore();
        var service = new ShapeMetadataService(cells);

        service.Write(new ShapeQualityMetadata(
            CodexId: "node-1",
            RegionId: "encoder",
            ObjectRole: "layer",
            LastModifiedRevision: 28));

        Assert.Equal("node-1", cells.Values["User.CodexId"]);
        Assert.Equal("test", cells.Values["User.VsiEdition"]);
        Assert.Equal("0.4.0-test.1", cells.Values["User.VsiProtocolVersion"]);
        Assert.Equal("encoder", cells.Values["User.VsiRegionId"]);
        Assert.Equal("layer", cells.Values["User.VsiObjectRole"]);
        Assert.Equal("28", cells.Values["User.VsiLastModifiedRevision"]);
    }

    private static string Hash(char value) => new(value, 64);

    private sealed class MemoryQualityCellStore : IQualityCellStore
    {
        public Dictionary<string, string> Values { get; } = new(StringComparer.Ordinal);

        public string? Read(string cellName) =>
            Values.TryGetValue(cellName, out var value) ? value : null;

        public void Write(string cellName, string value) =>
            Values[cellName] = value;
    }
}
