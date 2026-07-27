using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using VisioTestBridge.Contracts;

namespace VisioTestBridge.Core.Persistence;

public interface ISolutionXmlBackend
{
    string? Read(string solutionName);

    void Write(string solutionName, string xml);
}

public sealed record PersistedAuditEntry(
    long Revision,
    string? RegionId,
    string Profile,
    string EvidenceSha256);

public sealed record PersistedFindingState(
    string FindingId,
    AuditFindingStatus Status,
    string? WaiverReason);

public sealed record AuditHistoryState(
    IReadOnlyList<string> RegionIds,
    IReadOnlyList<PersistedAuditEntry> RegionAudits,
    IReadOnlyList<PersistedAuditEntry> WholePageAudits,
    IReadOnlyList<PersistedFindingState> Findings);

public sealed class AuditSolutionXmlStore(ISolutionXmlBackend backend)
{
    public const string SolutionName = "VisioScientificIllustratorTest.Audit.v1";

    public string Write(AuditHistoryState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var xml = Serialize(Bound(state));
        backend.Write(SolutionName, xml);
        var persisted = backend.Read(SolutionName)
            ?? throw Corrupt("写入后无法读取 SolutionXML。");
        Parse(persisted);
        return Hash(persisted);
    }

    public AuditHistoryState Read(string expectedSha256)
    {
        ValidateHash(expectedSha256);
        var xml = backend.Read(SolutionName);
        if (xml is null || !string.Equals(Hash(xml), expectedSha256, StringComparison.Ordinal))
            throw Corrupt("SolutionXML 缺失或哈希不匹配。");

        try
        {
            return Parse(xml);
        }
        catch (BridgeCommandException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is FormatException
            or InvalidOperationException
            or System.Xml.XmlException)
        {
            throw Corrupt($"SolutionXML 无法解析：{exception.Message}");
        }
    }

    public string ComputeCurrentHash()
    {
        var xml = backend.Read(SolutionName);
        if (xml is null)
            throw Corrupt("SolutionXML 不存在。");
        return Hash(xml);
    }

    private static AuditHistoryState Bound(AuditHistoryState state)
    {
        var regionIds = state.RegionIds
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var regionAudits = state.RegionAudits
            .OrderByDescending(entry => entry.Revision)
            .Take(20)
            .OrderBy(entry => entry.Revision)
            .ThenBy(entry => entry.RegionId, StringComparer.Ordinal)
            .ToArray();
        var wholePageAudits = state.WholePageAudits
            .OrderByDescending(entry => entry.Revision)
            .Take(5)
            .OrderBy(entry => entry.Revision)
            .ToArray();
        var findings = state.Findings
            .OrderBy(entry => entry.FindingId, StringComparer.Ordinal)
            .ToArray();

        foreach (var entry in regionAudits.Concat(wholePageAudits))
        {
            if (entry.Revision < 0
                || string.IsNullOrWhiteSpace(entry.Profile))
                throw Corrupt("审计历史条目无效。");
            ValidateHash(entry.EvidenceSha256);
        }
        foreach (var finding in findings)
        {
            if (string.IsNullOrWhiteSpace(finding.FindingId)
                || finding.Status == AuditFindingStatus.Waived
                && string.IsNullOrWhiteSpace(finding.WaiverReason))
            {
                throw Corrupt("finding 状态或 waiver 无效。");
            }
        }

        return new AuditHistoryState(regionIds, regionAudits, wholePageAudits, findings);
    }

    private static string Serialize(AuditHistoryState state)
    {
        var root = new XElement(
            "SolutionXML",
            new XAttribute("Name", SolutionName),
            new XElement(
                "visio-scientific-illustrator-test-audit",
                new XAttribute("version", "1"),
            new XElement(
                "regions",
                state.RegionIds.Select(regionId =>
                    new XElement("region", new XAttribute("id", regionId)))),
            new XElement(
                "region-audits",
                state.RegionAudits.Select(entry => AuditElement(entry, requireRegion: true))),
            new XElement(
                "whole-page-audits",
                state.WholePageAudits.Select(entry => AuditElement(entry, requireRegion: false))),
            new XElement(
                "findings",
                state.Findings.Select(finding =>
                {
                    var element = new XElement(
                        "finding",
                        new XAttribute("id", finding.FindingId),
                        new XAttribute("status", FormatStatus(finding.Status)));
                    if (finding.WaiverReason is not null)
                        element.Add(new XAttribute("waiver-reason", finding.WaiverReason));
                    return element;
                }))));
        return root.ToString(SaveOptions.DisableFormatting);
    }

    private static XElement AuditElement(PersistedAuditEntry entry, bool requireRegion)
    {
        if (requireRegion && string.IsNullOrWhiteSpace(entry.RegionId))
            throw Corrupt("区域审计条目缺少 region_id。");
        if (!requireRegion && entry.RegionId is not null)
            throw Corrupt("全图审计条目不能包含 region_id。");

        var element = new XElement(
            "audit",
            new XAttribute("revision", entry.Revision.ToString(CultureInfo.InvariantCulture)),
            new XAttribute("profile", entry.Profile),
            new XAttribute("evidence-sha256", entry.EvidenceSha256));
        if (entry.RegionId is not null)
            element.Add(new XAttribute("region-id", entry.RegionId));
        return element;
    }

    private static AuditHistoryState Parse(string xml)
    {
        var root = XElement.Parse(xml, LoadOptions.None);
        if (root.Name.LocalName != "SolutionXML"
            || (string?)root.Attribute("Name") != SolutionName)
            throw Corrupt("SolutionXML 根节点或名称无效。");
        var payload = RequiredElement(root, "visio-scientific-illustrator-test-audit");
        if ((string?)payload.Attribute("version") != "1")
            throw Corrupt("SolutionXML 根节点或版本无效。");

        var regions = RequiredElement(payload, "regions")
            .Elements("region")
            .Select(element => RequiredAttribute(element, "id"))
            .ToArray();
        var regionAudits = RequiredElement(payload, "region-audits")
            .Elements("audit")
            .Select(element => ParseAudit(element, requireRegion: true))
            .ToArray();
        var wholeAudits = RequiredElement(payload, "whole-page-audits")
            .Elements("audit")
            .Select(element => ParseAudit(element, requireRegion: false))
            .ToArray();
        var findings = RequiredElement(payload, "findings")
            .Elements("finding")
            .Select(element => new PersistedFindingState(
                RequiredAttribute(element, "id"),
                ParseStatus(RequiredAttribute(element, "status")),
                (string?)element.Attribute("waiver-reason")))
            .ToArray();

        return Bound(new AuditHistoryState(regions, regionAudits, wholeAudits, findings));
    }

    private static PersistedAuditEntry ParseAudit(XElement element, bool requireRegion)
    {
        var revision = long.Parse(
            RequiredAttribute(element, "revision"),
            NumberStyles.None,
            CultureInfo.InvariantCulture);
        var regionId = (string?)element.Attribute("region-id");
        if (requireRegion && string.IsNullOrWhiteSpace(regionId)
            || !requireRegion && regionId is not null)
            throw Corrupt("审计历史的区域属性无效。");
        return new PersistedAuditEntry(
            revision,
            regionId,
            RequiredAttribute(element, "profile"),
            RequiredAttribute(element, "evidence-sha256"));
    }

    private static XElement RequiredElement(XElement parent, string name) =>
        parent.Element(name) ?? throw Corrupt($"SolutionXML 缺少 {name}。");

    private static string RequiredAttribute(XElement element, string name)
    {
        var value = (string?)element.Attribute(name);
        return string.IsNullOrWhiteSpace(value)
            ? throw Corrupt($"SolutionXML 缺少属性 {name}。")
            : value;
    }

    private static string FormatStatus(AuditFindingStatus status) => status switch
    {
        AuditFindingStatus.Open => "open",
        AuditFindingStatus.Resolved => "resolved",
        AuditFindingStatus.Waived => "waived",
        AuditFindingStatus.Stale => "stale",
        _ => throw Corrupt($"未知 finding 状态：{status}"),
    };

    private static AuditFindingStatus ParseStatus(string value) => value switch
    {
        "open" => AuditFindingStatus.Open,
        "resolved" => AuditFindingStatus.Resolved,
        "waived" => AuditFindingStatus.Waived,
        "stale" => AuditFindingStatus.Stale,
        _ => throw Corrupt($"未知 finding 状态：{value}"),
    };

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static void ValidateHash(string value)
    {
        if (value is null
            || value.Length != 64
            || value.Any(character =>
                character is not (>= '0' and <= '9')
                and not (>= 'a' and <= 'f')))
            throw Corrupt("审计历史哈希必须是 64 位小写 SHA-256。");
    }

    private static BridgeCommandException Corrupt(string message) =>
        new(BridgeErrorCodes.AuditStateCorrupt, message);
}
