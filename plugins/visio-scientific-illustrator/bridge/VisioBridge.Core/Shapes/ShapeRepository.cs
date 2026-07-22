using System.Text;
using VisioBridge.Core.Abstractions;

namespace VisioBridge.Core.Shapes;

public sealed class ShapeRepository
{
    private readonly IShapeAccessor _accessor;

    public ShapeRepository(IShapeAccessor accessor)
    {
        _accessor = accessor ?? throw new ArgumentNullException(nameof(accessor));
    }

    public IShapeHandle Resolve(string semanticId)
    {
        ValidateSemanticId(semanticId);
        var shapes = _accessor.EnumerateBoundPageShapesRecursively();
        var matches = new List<IShapeHandle>();
        foreach (var shape in shapes)
        {
            if (string.Equals(
                _accessor.ReadSemanticId(shape),
                semanticId,
                StringComparison.Ordinal))
            {
                matches.Add(shape);
            }
            else
            {
                shape.Dispose();
            }
        }

        if (matches.Count == 1) return matches[0];
        foreach (var match in matches) match.Dispose();
        if (matches.Count == 0)
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.ShapeNotFound,
                $"未找到 semantic_id: {semanticId}");
        }
        throw Duplicate(semanticId);
    }

    public void Register(IShapeHandle shape, string semanticId)
    {
        ArgumentNullException.ThrowIfNull(shape);
        ValidateSemanticId(semanticId);
        var nameU = ToUniversalName(semanticId);
        var shapes = _accessor.EnumerateBoundPageShapesRecursively();
        try
        {
            foreach (var existing in shapes)
            {
                if (existing.Id == shape.Id) continue;
                if (string.Equals(
                        _accessor.ReadSemanticId(existing),
                        semanticId,
                        StringComparison.Ordinal)
                    || string.Equals(existing.NameU, nameU, StringComparison.Ordinal))
                {
                    throw Duplicate(semanticId);
                }
            }
        }
        finally
        {
            foreach (var existing in shapes) existing.Dispose();
        }

        _accessor.WriteSemanticId(shape, semanticId);
        _accessor.SetUniversalName(shape, nameU);
    }

    public static string ToUniversalName(string semanticId)
    {
        ValidateSemanticId(semanticId);
        var builder = new StringBuilder("Codex_", semanticId.Length + 6);
        foreach (var character in semanticId)
        {
            var asciiAlphaNumeric = character is >= 'a' and <= 'z'
                or >= 'A' and <= 'Z'
                or >= '0' and <= '9';
            builder.Append(asciiAlphaNumeric || character == '_' ? character : '_');
        }
        return builder.ToString();
    }

    private static void ValidateSemanticId(string semanticId)
    {
        if (string.IsNullOrWhiteSpace(semanticId)
            || semanticId.Length > 120
            || semanticId.Any(char.IsControl))
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.InvalidArgument,
                "semantic_id 必须为 1–120 个非控制字符。");
        }
    }

    private static BridgeCommandException Duplicate(string semanticId) => new(
        BridgeErrorCodes.DuplicateSemanticId,
        $"semantic_id 或 NameU 冲突: {semanticId}");
}
