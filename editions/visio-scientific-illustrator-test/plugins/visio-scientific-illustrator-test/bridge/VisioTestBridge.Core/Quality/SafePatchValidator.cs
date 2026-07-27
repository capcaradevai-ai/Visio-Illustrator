using VisioTestBridge.Contracts;

namespace VisioTestBridge.Core.Quality;

public static class SafePatchValidator
{
    private static readonly HashSet<string> AllowedOperations = new(
    [
        "expand_text_box",
        "move_inside_canvas",
    ],
    StringComparer.Ordinal);

    public static AuditSafePatch Validate(AuditSafePatch patch)
    {
        ArgumentNullException.ThrowIfNull(patch);
        if (!AllowedOperations.Contains(patch.Operation)
            || patch.ObjectIds is null
            || patch.ObjectIds.Count == 0
            || patch.ObjectIds.Any(string.IsNullOrWhiteSpace)
            || patch.ObjectIds.Distinct(StringComparer.Ordinal).Count() != patch.ObjectIds.Count
            || patch.ExpectedRevision < 0
            || patch.Arguments is null)
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.ComponentContractInvalid,
                "safe patch 操作、对象、修订号或参数无效。");
        }
        return patch;
    }
}
