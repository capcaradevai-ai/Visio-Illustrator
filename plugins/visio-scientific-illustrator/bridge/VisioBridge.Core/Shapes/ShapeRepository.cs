using System.Text;
using VisioBridge.Core.Abstractions;

namespace VisioBridge.Core.Shapes;

public sealed class ShapeRepository
{
    private readonly IShapeAccessor _accessor;
    private HashSet<string>? _semanticIds;

    public ShapeRepository(IShapeAccessor accessor)
    {
        _accessor = accessor ?? throw new ArgumentNullException(nameof(accessor));
    }

    public IShapeHandle Resolve(string semanticId)
    {
        ValidateSemanticId(semanticId);
        EnsureIndexed();
        if (!_semanticIds!.Contains(semanticId))
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.ShapeNotFound,
                $"未找到 semantic_id: {semanticId}");
        }
        var shape = _accessor.FindByUniversalName(ToUniversalName(semanticId));
        if (shape is null)
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.ShapeNotFound,
                $"未找到 semantic_id: {semanticId}");
        }
        if (!string.Equals(_accessor.ReadSemanticId(shape), semanticId, StringComparison.Ordinal))
        {
            shape.Dispose();
            throw Duplicate(semanticId);
        }
        return shape;
    }

    public void Register(IShapeHandle shape, string semanticId)
    {
        ArgumentNullException.ThrowIfNull(shape);
        ValidateSemanticId(semanticId);
        EnsureIndexed();
        if (_semanticIds!.Contains(semanticId)) throw Duplicate(semanticId);
        var nameU = ToUniversalName(semanticId);
        _accessor.WriteSemanticId(shape, semanticId);
        _accessor.SetUniversalName(shape, nameU);
        if (!string.Equals(shape.NameU, nameU, StringComparison.Ordinal))
        {
            throw Duplicate(semanticId);
        }
        _semanticIds.Add(semanticId);
    }

    public IShapeHandle Adopt(int shapeId, string semanticId)
    {
        if (shapeId <= 0)
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.InvalidArgument,
                "shape_id 必须为正整数。");
        }

        ValidateSemanticId(semanticId);
        RebuildIndex();

        var shape = _accessor.FindById(shapeId);
        if (shape is null)
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.ShapeNotFound,
                $"未找到 shape_id: {shapeId}");
        }

        try
        {
            var existingSemanticId = _accessor.ReadSemanticId(shape);
            if (!string.IsNullOrEmpty(existingSemanticId)
                && !string.Equals(existingSemanticId, semanticId, StringComparison.Ordinal))
            {
                throw new BridgeCommandException(
                    BridgeErrorCodes.InvalidArgument,
                    $"shape_id {shapeId} 已具有其他 semantic_id: {existingSemanticId}");
            }

            var nameU = ToUniversalName(semanticId);
            using (var existingByName = _accessor.FindByUniversalName(nameU))
            {
                if (existingByName is not null && existingByName.Id != shapeId)
                {
                    throw Duplicate(semanticId);
                }
            }

            var allShapes = _accessor.EnumerateBoundPageShapesRecursively();
            try
            {
                foreach (var candidate in allShapes)
                {
                    if (candidate.Id == shapeId) continue;
                    if (string.Equals(
                            _accessor.ReadSemanticId(candidate),
                            semanticId,
                            StringComparison.Ordinal))
                    {
                        throw Duplicate(semanticId);
                    }
                }
            }
            finally
            {
                foreach (var candidate in allShapes) candidate.Dispose();
            }

            _accessor.WriteSemanticId(shape, semanticId);
            _accessor.SetUniversalName(shape, nameU);

            if (!string.Equals(shape.NameU, nameU, StringComparison.Ordinal)
                || !string.Equals(
                    _accessor.ReadSemanticId(shape),
                    semanticId,
                    StringComparison.Ordinal))
            {
                throw Duplicate(semanticId);
            }

            RebuildIndex();
            return shape;
        }
        catch
        {
            shape.Dispose();
            throw;
        }
    }

    public void RebuildIndex()
    {
        var semanticIds = new HashSet<string>(StringComparer.Ordinal);
        var shapes = _accessor.EnumerateBoundPageShapesRecursively();
        try
        {
            foreach (var shape in shapes)
            {
                var semanticId = _accessor.ReadSemanticId(shape);
                if (string.IsNullOrEmpty(semanticId)) continue;
                if (!semanticIds.Add(semanticId)) throw Duplicate(semanticId);
            }
        }
        finally
        {
            foreach (var shape in shapes) shape.Dispose();
        }
        _semanticIds = semanticIds;
    }

    private void EnsureIndexed()
    {
        if (_semanticIds is null) RebuildIndex();
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