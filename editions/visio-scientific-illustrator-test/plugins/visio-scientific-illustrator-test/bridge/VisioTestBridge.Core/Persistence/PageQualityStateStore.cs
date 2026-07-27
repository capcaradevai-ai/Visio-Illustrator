using System.Globalization;
using VisioTestBridge.Contracts;

namespace VisioTestBridge.Core.Persistence;

public interface IQualityCellStore
{
    string? Read(string cellName);

    void Write(string cellName, string value);
}

public sealed record PageQualityState(
    string ProtocolVersion,
    long PageRevision,
    string CurrentRegion,
    RegionQualityState QualityState,
    long? LastPassedRevision,
    string PreviewSha256,
    string StructureSha256,
    string SolutionXmlSha256);

public sealed class PageQualityStateStore(IQualityCellStore cells)
{
    public static readonly IReadOnlyList<string> RequiredCells =
    [
        "User.VsiProtocolVersion",
        "User.VsiPageRevision",
        "User.VsiCurrentRegion",
        "User.VsiQualityState",
        "User.VsiLastPassedRevision",
        "User.VsiPreviewSha256",
        "User.VsiStructureSha256",
        "User.VsiSolutionXmlSha256",
    ];

    public PageQualityState Read()
    {
        var protocolVersion = Required("User.VsiProtocolVersion");
        var revisionText = Required("User.VsiPageRevision");
        var regionId = Required("User.VsiCurrentRegion");
        var stateText = Required("User.VsiQualityState");
        var lastPassedText = RequiredAllowEmpty("User.VsiLastPassedRevision");
        var previewHash = Required("User.VsiPreviewSha256");
        var structureHash = Required("User.VsiStructureSha256");
        var solutionXmlHash = Required("User.VsiSolutionXmlSha256");

        if (!long.TryParse(revisionText, NumberStyles.None, CultureInfo.InvariantCulture, out var revision)
            || revision < 0)
        {
            throw Corrupt("User.VsiPageRevision 必须是非负整数。");
        }

        long? lastPassedRevision = null;
        if (lastPassedText.Length > 0)
        {
            if (!long.TryParse(lastPassedText, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
                || parsed < 0
                || parsed > revision)
            {
                throw Corrupt("User.VsiLastPassedRevision 无效。");
            }
            lastPassedRevision = parsed;
        }

        var qualityState = ParseState(stateText);
        ValidateHash(previewHash, "User.VsiPreviewSha256");
        ValidateHash(structureHash, "User.VsiStructureSha256");
        ValidateHash(solutionXmlHash, "User.VsiSolutionXmlSha256");

        return new PageQualityState(
            protocolVersion,
            revision,
            regionId,
            qualityState,
            lastPassedRevision,
            previewHash,
            structureHash,
            solutionXmlHash);
    }

    public void Write(PageQualityState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        Validate(state);

        cells.Write("User.VsiProtocolVersion", state.ProtocolVersion);
        cells.Write("User.VsiPageRevision", state.PageRevision.ToString(CultureInfo.InvariantCulture));
        cells.Write("User.VsiCurrentRegion", state.CurrentRegion);
        cells.Write("User.VsiQualityState", FormatState(state.QualityState));
        cells.Write(
            "User.VsiLastPassedRevision",
            state.LastPassedRevision?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
        cells.Write("User.VsiPreviewSha256", state.PreviewSha256);
        cells.Write("User.VsiStructureSha256", state.StructureSha256);
        cells.Write("User.VsiSolutionXmlSha256", state.SolutionXmlSha256);
    }

    private static void Validate(PageQualityState state)
    {
        if (string.IsNullOrWhiteSpace(state.ProtocolVersion))
            throw Corrupt("协议版本不能为空。");
        if (state.PageRevision < 0)
            throw Corrupt("页面修订号不能为负数。");
        if (string.IsNullOrWhiteSpace(state.CurrentRegion))
            throw Corrupt("当前区域不能为空。");
        if (state.LastPassedRevision is < 0 || state.LastPassedRevision > state.PageRevision)
            throw Corrupt("最后通过修订号无效。");
        ValidateHash(state.PreviewSha256, "User.VsiPreviewSha256");
        ValidateHash(state.StructureSha256, "User.VsiStructureSha256");
        ValidateHash(state.SolutionXmlSha256, "User.VsiSolutionXmlSha256");
    }

    private string Required(string cellName)
    {
        var value = cells.Read(cellName);
        if (string.IsNullOrWhiteSpace(value))
            throw Corrupt($"缺少或为空的质量状态单元格：{cellName}");
        return value;
    }

    private string RequiredAllowEmpty(string cellName)
    {
        var value = cells.Read(cellName);
        if (value is null)
            throw Corrupt($"缺少质量状态单元格：{cellName}");
        return value;
    }

    private static RegionQualityState ParseState(string value) => value switch
    {
        "planned" => RegionQualityState.Planned,
        "drawing" => RegionQualityState.Drawing,
        "awaiting_audit" => RegionQualityState.AwaitingAudit,
        "failed" => RegionQualityState.Failed,
        "correcting" => RegionQualityState.Correcting,
        "passed" => RegionQualityState.Passed,
        _ => throw Corrupt($"未知质量状态：{value}"),
    };

    private static string FormatState(RegionQualityState value) => value switch
    {
        RegionQualityState.Planned => "planned",
        RegionQualityState.Drawing => "drawing",
        RegionQualityState.AwaitingAudit => "awaiting_audit",
        RegionQualityState.Failed => "failed",
        RegionQualityState.Correcting => "correcting",
        RegionQualityState.Passed => "passed",
        _ => throw Corrupt($"未知质量状态枚举：{value}"),
    };

    private static void ValidateHash(string value, string cellName)
    {
        if (value.Length != 64 || value.Any(character =>
                character is not (>= '0' and <= '9')
                and not (>= 'a' and <= 'f')))
        {
            throw Corrupt($"{cellName} 必须是 64 位小写 SHA-256。");
        }
    }

    private static BridgeCommandException Corrupt(string message) =>
        new(BridgeErrorCodes.AuditStateCorrupt, message);
}
