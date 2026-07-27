using VisioTestBridge.Contracts;
using VisioTestBridge.Core;
using VisioTestBridge.Core.Persistence;

namespace VisioTestBridge.Tests;

public sealed class AuditSolutionXmlStoreTests
{
    [Fact]
    public void Persists_bounded_canonical_history_and_validates_hash()
    {
        var backend = new MemorySolutionXmlBackend();
        var store = new AuditSolutionXmlStore(backend);
        var state = new AuditHistoryState(
            RegionIds: ["encoder", "decoder"],
            RegionAudits: Enumerable.Range(1, 22)
                .Select(revision => Entry(revision, "encoder"))
                .ToArray(),
            WholePageAudits: Enumerable.Range(1, 7)
                .Select(revision => Entry(revision, null))
                .ToArray(),
            Findings:
            [
                new PersistedFindingState(
                    "connector_glue:encoder:flow",
                    AuditFindingStatus.Waived,
                    "reference intentionally omits arrowhead"),
            ]);

        var hash = store.Write(state);
        var restored = store.Read(hash);

        Assert.Equal("VisioScientificIllustratorTest.Audit.v1", backend.SolutionName);
        Assert.Equal(20, restored.RegionAudits.Count);
        Assert.Equal(5, restored.WholePageAudits.Count);
        Assert.Equal(3, restored.RegionAudits.Min(entry => entry.Revision));
        Assert.Equal(3, restored.WholePageAudits.Min(entry => entry.Revision));
        Assert.Equal(64, hash.Length);
        Assert.Equal(hash, store.ComputeCurrentHash());
    }

    [Fact]
    public void Rejects_tampered_or_missing_history_instead_of_creating_empty_state()
    {
        var backend = new MemorySolutionXmlBackend();
        var store = new AuditSolutionXmlStore(backend);

        var missing = Assert.Throws<BridgeCommandException>(() => store.Read(new string('a', 64)));
        Assert.Equal(BridgeErrorCodes.AuditStateCorrupt, missing.Code);

        var hash = store.Write(new AuditHistoryState(["main"], [], [], []));
        backend.Xml += " ";

        var tampered = Assert.Throws<BridgeCommandException>(() => store.Read(hash));
        Assert.Equal(BridgeErrorCodes.AuditStateCorrupt, tampered.Code);
    }

    private static PersistedAuditEntry Entry(long revision, string? regionId) =>
        new(revision, regionId, "adaptive-v1", new string('a', 64));

    private sealed class MemorySolutionXmlBackend : ISolutionXmlBackend
    {
        public string? SolutionName { get; private set; }

        public string? Xml { get; set; }

        public string? Read(string solutionName)
        {
            SolutionName = solutionName;
            return Xml;
        }

        public void Write(string solutionName, string xml)
        {
            SolutionName = solutionName;
            Xml = xml;
        }
    }
}
