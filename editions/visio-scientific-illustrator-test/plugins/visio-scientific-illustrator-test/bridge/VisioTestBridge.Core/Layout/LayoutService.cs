using VisioTestBridge.Contracts;
using System.Runtime.InteropServices;
using VisioTestBridge.Core.Geometry;
using VisioTestBridge.Core.Inspection;
using VisioTestBridge.Core.Shapes;
using Visio = Microsoft.Office.Interop.Visio;

namespace VisioTestBridge.Core.Layout;

internal sealed record LayoutShapeState(string SemanticId, PixelBox Bounds, int ZOrder);

internal static class LayoutPlanner
{
    private static readonly HashSet<string> Alignments =
        ["left", "center", "right", "top", "middle", "bottom"];

    public static IReadOnlyDictionary<string, PixelBox> Align(
        IReadOnlyList<LayoutShapeState> shapes,
        string alignment,
        string? referenceSemanticId)
    {
        ValidateStates(shapes, minimum: 2);
        if (!Alignments.Contains(alignment))
            throw Invalid("alignment 仅允许 left、center、right、top、middle 或 bottom。");
        var reference = FindReference(shapes, referenceSemanticId ?? shapes[0].SemanticId);
        return shapes.ToDictionary(
            shape => shape.SemanticId,
            shape => AlignBounds(shape.Bounds, reference.Bounds, alignment),
            StringComparer.Ordinal);
    }

    public static IReadOnlyDictionary<string, PixelBox> Distribute(
        IReadOnlyList<LayoutShapeState> shapes,
        string axis,
        string distribution,
        bool preserveOuterBounds)
    {
        ValidateStates(shapes, minimum: 3);
        if (axis is not ("horizontal" or "vertical"))
            throw Invalid("axis 仅允许 horizontal 或 vertical。");
        if (distribution is not ("equal_gap" or "equal_center"))
            throw Invalid("distribution 仅允许 equal_gap 或 equal_center。");

        var horizontal = axis == "horizontal";
        var ordered = shapes.OrderBy(shape => horizontal
                ? shape.Bounds.X + shape.Bounds.Width / 2
                : shape.Bounds.Y + shape.Bounds.Height / 2)
            .ToArray();
        var desired = shapes.ToDictionary(
            shape => shape.SemanticId,
            shape => shape.Bounds,
            StringComparer.Ordinal);
        if (distribution == "equal_center")
            DistributeCenters(ordered, desired, horizontal, preserveOuterBounds);
        else
            DistributeGaps(ordered, desired, horizontal, preserveOuterBounds);
        return desired;
    }

    public static IReadOnlyDictionary<string, PixelBox> MatchSize(
        IReadOnlyList<LayoutShapeState> shapes,
        string referenceSemanticId,
        string dimension)
    {
        ValidateStates(shapes, minimum: 2);
        if (dimension is not ("width" or "height" or "both"))
            throw Invalid("dimension 仅允许 width、height 或 both。");
        var reference = FindReference(shapes, referenceSemanticId);
        return shapes.ToDictionary(
            shape => shape.SemanticId,
            shape => new PixelBox(
                shape.Bounds.X,
                shape.Bounds.Y,
                dimension is "width" or "both" ? reference.Bounds.Width : shape.Bounds.Width,
                dimension is "height" or "both" ? reference.Bounds.Height : shape.Bounds.Height),
            StringComparer.Ordinal);
    }

    public static IReadOnlyDictionary<string, string> CompileProtection(ProtectionFields fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        var cells = new Dictionary<string, string>(StringComparer.Ordinal);
        Add(cells, "LockMoveX", fields.MoveX);
        Add(cells, "LockMoveY", fields.MoveY);
        Add(cells, "LockWidth", fields.Width);
        Add(cells, "LockHeight", fields.Height);
        Add(cells, "LockRotate", fields.Rotate);
        Add(cells, "LockTextEdit", fields.TextEdit);
        Add(cells, "LockDelete", fields.Delete);
        Add(cells, "LockFormat", fields.Format);
        Add(cells, "LockGroup", fields.Group);
        Add(cells, "LockSelect", fields.Select);
        Add(cells, "LockAspect", fields.Aspect);
        Add(cells, "LockVtxEdit", fields.VertexEdit);
        Add(cells, "LockBegin", fields.Begin);
        Add(cells, "LockEnd", fields.End);
        if (cells.Count == 0) throw Invalid("fields 至少需要一个保护字段。");
        return cells;
    }

    public static void ValidateZOrder(SetZOrderCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Action is not ("bring_to_front" or "send_to_back" or "move_forward" or "move_backward" or "absolute"))
            throw Invalid("action 不是有效的 Visio Z-order 操作。");
        if (command.Action == "absolute" && command.Position is null
            || command.Action != "absolute" && command.Position is not null
            || command.Position is < 1)
            throw Invalid("absolute action 必须且只能提供大于零的 position。");
    }

    public static void ValidateSemanticIds(IReadOnlyList<string> semanticIds, int minimum)
    {
        ArgumentNullException.ThrowIfNull(semanticIds);
        if (semanticIds.Count < minimum
            || semanticIds.Any(string.IsNullOrWhiteSpace)
            || semanticIds.Distinct(StringComparer.Ordinal).Count() != semanticIds.Count)
            throw Invalid($"semantic_ids 至少需要 {minimum} 个非空且唯一的 ID。");
    }

    private static PixelBox AlignBounds(PixelBox current, PixelBox reference, string alignment) => alignment switch
    {
        "left" => current with { X = reference.X },
        "center" => current with { X = reference.X + (reference.Width - current.Width) / 2 },
        "right" => current with { X = reference.X + reference.Width - current.Width },
        "top" => current with { Y = reference.Y },
        "middle" => current with { Y = reference.Y + (reference.Height - current.Height) / 2 },
        "bottom" => current with { Y = reference.Y + reference.Height - current.Height },
        _ => throw new ArgumentOutOfRangeException(nameof(alignment)),
    };

    private static void DistributeCenters(
        IReadOnlyList<LayoutShapeState> ordered,
        IDictionary<string, PixelBox> desired,
        bool horizontal,
        bool preserveOuterBounds)
    {
        var centers = ordered.Select(shape => horizontal
            ? shape.Bounds.X + shape.Bounds.Width / 2
            : shape.Bounds.Y + shape.Bounds.Height / 2).ToArray();
        var step = preserveOuterBounds
            ? (centers[^1] - centers[0]) / (ordered.Count - 1)
            : centers.Zip(centers.Skip(1), (left, right) => right - left).Average();
        for (var index = 1; index < ordered.Count; index++)
        {
            if (preserveOuterBounds && index == ordered.Count - 1) continue;
            var shape = ordered[index];
            var center = centers[0] + step * index;
            desired[shape.SemanticId] = horizontal
                ? shape.Bounds with { X = center - shape.Bounds.Width / 2 }
                : shape.Bounds with { Y = center - shape.Bounds.Height / 2 };
        }
    }

    private static void DistributeGaps(
        IReadOnlyList<LayoutShapeState> ordered,
        IDictionary<string, PixelBox> desired,
        bool horizontal,
        bool preserveOuterBounds)
    {
        var first = horizontal ? ordered[0].Bounds.X : ordered[0].Bounds.Y;
        double gap;
        if (preserveOuterBounds)
        {
            var lastEnd = horizontal
                ? ordered[^1].Bounds.X + ordered[^1].Bounds.Width
                : ordered[^1].Bounds.Y + ordered[^1].Bounds.Height;
            var totalSize = ordered.Sum(shape => horizontal ? shape.Bounds.Width : shape.Bounds.Height);
            gap = (lastEnd - first - totalSize) / (ordered.Count - 1);
        }
        else
        {
            gap = ordered.Zip(ordered.Skip(1), (left, right) => horizontal
                    ? right.Bounds.X - left.Bounds.X - left.Bounds.Width
                    : right.Bounds.Y - left.Bounds.Y - left.Bounds.Height)
                .Average();
        }

        var cursor = first;
        for (var index = 0; index < ordered.Count; index++)
        {
            var shape = ordered[index];
            if (!(preserveOuterBounds && (index == 0 || index == ordered.Count - 1)))
            {
                desired[shape.SemanticId] = horizontal
                    ? shape.Bounds with { X = cursor }
                    : shape.Bounds with { Y = cursor };
            }
            cursor += (horizontal ? shape.Bounds.Width : shape.Bounds.Height) + gap;
        }
    }

    private static LayoutShapeState FindReference(
        IReadOnlyList<LayoutShapeState> shapes,
        string semanticId) => shapes.SingleOrDefault(shape => shape.SemanticId == semanticId)
        ?? throw Invalid($"reference_semantic_id 不在 semantic_ids 中: {semanticId}");

    private static void ValidateStates(IReadOnlyList<LayoutShapeState> shapes, int minimum)
    {
        ArgumentNullException.ThrowIfNull(shapes);
        ValidateSemanticIds(shapes.Select(shape => shape.SemanticId).ToArray(), minimum);
    }

    private static void Add(IDictionary<string, string> cells, string name, bool? value)
    {
        if (value is not null) cells[name] = value.Value ? "1" : "0";
    }

    private static BridgeCommandException Invalid(string message) =>
        new(BridgeErrorCodes.InvalidArgument, message);
}

internal sealed class LayoutService
{
    private readonly Visio.Page _page;
    private readonly ShapeRepository _repository;
    private readonly PageInspector _inspector;
    private readonly VisioShapeEditorAdapter _editor;

    public LayoutService(
        Visio.Page page,
        ShapeRepository repository,
        GeometryConverter geometry,
        PageInspector inspector)
    {
        _page = page ?? throw new ArgumentNullException(nameof(page));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _inspector = inspector ?? throw new ArgumentNullException(nameof(inspector));
        _editor = new VisioShapeEditorAdapter(page, repository, geometry, inspector);
    }

    public LayoutMutationResult Align(AlignShapesCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var before = ReadStates(command.SemanticIds);
        var desired = LayoutPlanner.Align(before, command.Alignment, command.ReferenceSemanticId);
        ApplyBounds(before, desired);
        return Result("align_shapes", before, command.SemanticIds);
    }

    public LayoutMutationResult Distribute(DistributeShapesCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var before = ReadStates(command.SemanticIds);
        var desired = LayoutPlanner.Distribute(
            before,
            command.Axis,
            command.Distribution,
            command.PreserveOuterBounds);
        ApplyBounds(before, desired);
        return Result("distribute_shapes", before, command.SemanticIds);
    }

    public LayoutMutationResult MatchSize(MatchSizeCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var before = ReadStates(command.SemanticIds);
        var desired = LayoutPlanner.MatchSize(before, command.ReferenceSemanticId, command.Dimension);
        ApplyBounds(before, desired);
        return Result("match_size", before, command.SemanticIds);
    }

    public LayoutMutationResult SetProtection(SetProtectionCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        LayoutPlanner.ValidateSemanticIds(command.SemanticIds, 1);
        var formulas = LayoutPlanner.CompileProtection(command.Fields);
        var before = ReadStates(command.SemanticIds);
        foreach (var semanticId in command.SemanticIds)
        {
            using var handle = _repository.Resolve(semanticId);
            var shape = ((VisioShapeHandle)handle).Shape;
            foreach (var formula in formulas)
            {
                StyleMapper.SetFormula(shape, formula.Key, formula.Value);
            }
        }
        return Result("set_protection", before, command.SemanticIds);
    }

    public LayoutMutationResult AssignLayer(AssignLayerCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        LayoutPlanner.ValidateSemanticIds(command.SemanticIds, 1);
        if (string.IsNullOrWhiteSpace(command.Layer) || command.Layer.Length > 255)
            throw Invalid("layer 必须是 1–255 个字符的名称。");

        var before = ReadStates(command.SemanticIds);
        var layer = ResolveLayer(command.Layer, command.CreateIfMissing);
        try
        {
            foreach (var semanticId in command.SemanticIds)
            {
                using var handle = _repository.Resolve(semanticId);
                var shape = ((VisioShapeHandle)handle).Shape;
                if (command.ReplaceExisting) RemoveAllLayers(shape);
                layer.Add(shape, 0);
            }
        }
        finally
        {
            Marshal.FinalReleaseComObject(layer);
        }
        return Result("assign_layer", before, command.SemanticIds);
    }

    public LayoutMutationResult SetZOrder(SetZOrderCommand command)
    {
        LayoutPlanner.ValidateZOrder(command);
        var before = ReadStates([command.SemanticId]);
        using (var handle = _repository.Resolve(command.SemanticId))
        {
            var shape = ((VisioShapeHandle)handle).Shape;
            switch (command.Action)
            {
                case "bring_to_front": shape.BringToFront(); break;
                case "send_to_back": shape.SendToBack(); break;
                case "move_forward": shape.BringForward(); break;
                case "move_backward": shape.SendBackward(); break;
                case "absolute": MoveToAbsolutePosition(shape, command.Position!.Value); break;
            }
        }
        return Result("set_z_order", before, [command.SemanticId]);
    }

    public LayoutMutationResult Duplicate(DuplicateShapeCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (string.IsNullOrWhiteSpace(command.NewSemanticId))
            throw Invalid("new_semantic_id 不能为空。");
        if (!double.IsFinite(command.OffsetX) || !double.IsFinite(command.OffsetY))
            throw Invalid("offset_x 和 offset_y 必须是有限数值。");

        var source = ReadStates([command.SemanticId])[0];
        Visio.Shape? duplicate = null;
        var childMap = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            using var sourceHandle = _repository.Resolve(command.SemanticId);
            duplicate = ((VisioShapeHandle)sourceHandle).Shape.Duplicate();
            using (var duplicateHandle = new VisioShapeHandle(duplicate))
            {
                _repository.Register(duplicateHandle, command.NewSemanticId);
                RewriteChildSemanticIds(duplicate, command.NewSemanticId, childMap);
                duplicate = null;
            }
        }
        finally
        {
            if (duplicate is not null) Marshal.FinalReleaseComObject(duplicate);
        }

        _editor.Apply(
            new UpdateShapeCommand(
                command.NewSemanticId,
                X: source.Bounds.X + command.OffsetX,
                Y: source.Bounds.Y + command.OffsetY),
            source.Bounds with
            {
                X = source.Bounds.X + command.OffsetX,
                Y = source.Bounds.Y + command.OffsetY,
            });
        var after = ReadStates([command.NewSemanticId])[0];
        return new LayoutMutationResult(
            "duplicate_shape",
            [new LayoutShapeChange(
                command.NewSemanticId,
                source.Bounds,
                after.Bounds,
                source.ZOrder,
                after.ZOrder)],
            childMap.Count == 0 ? null : childMap);
    }

    private IReadOnlyList<LayoutShapeState> ReadStates(IReadOnlyList<string> semanticIds)
    {
        LayoutPlanner.ValidateSemanticIds(semanticIds, 1);
        var states = new List<LayoutShapeState>(semanticIds.Count);
        foreach (var semanticId in semanticIds)
        {
            using var handle = _repository.Resolve(semanticId);
            var shape = ((VisioShapeHandle)handle).Shape;
            states.Add(new LayoutShapeState(
                semanticId,
                _inspector.ReadBounds(shape),
                shape.Index));
        }
        return states;
    }

    private void ApplyBounds(
        IReadOnlyList<LayoutShapeState> before,
        IReadOnlyDictionary<string, PixelBox> desired)
    {
        foreach (var state in before)
        {
            var bounds = desired[state.SemanticId];
            if (bounds == state.Bounds) continue;
            _editor.Apply(
                new UpdateShapeCommand(
                    state.SemanticId,
                    X: bounds.X,
                    Y: bounds.Y,
                    Width: bounds.Width,
                    Height: bounds.Height),
                bounds);
        }
    }

    private LayoutMutationResult Result(
        string operation,
        IReadOnlyList<LayoutShapeState> before,
        IReadOnlyList<string> semanticIds)
    {
        var beforeById = before.ToDictionary(item => item.SemanticId, StringComparer.Ordinal);
        var after = ReadStates(semanticIds);
        return new LayoutMutationResult(
            operation,
            after.Select(item => new LayoutShapeChange(
                item.SemanticId,
                beforeById[item.SemanticId].Bounds,
                item.Bounds,
                beforeById[item.SemanticId].ZOrder,
                item.ZOrder)).ToArray());
    }

    private Visio.Layer ResolveLayer(string requestedName, bool createIfMissing)
    {
        var layers = _page.Layers;
        try
        {
            for (var index = 1; index <= layers.Count; index++)
            {
                var candidate = layers[index];
                if (string.Equals(candidate.NameU, requestedName, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(candidate.Name, requestedName, StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
                Marshal.FinalReleaseComObject(candidate);
            }
            if (!createIfMissing)
                throw Invalid($"页面中不存在图层：{requestedName}");
            return layers.Add(requestedName);
        }
        finally
        {
            Marshal.FinalReleaseComObject(layers);
        }
    }

    private static void RemoveAllLayers(Visio.Shape shape)
    {
        while (shape.LayerCount > 0)
        {
            var layer = shape.Layer[1];
            try
            {
                layer.Remove(shape, 0);
            }
            finally
            {
                Marshal.FinalReleaseComObject(layer);
            }
        }
    }

    private static void MoveToAbsolutePosition(Visio.Shape shape, int position)
    {
        var count = ReadSiblingCount(shape);
        if (position > count)
            throw Invalid($"position 超出同级形状数量 {count}。");
        shape.SendToBack();
        for (var index = 1; index < position; index++) shape.BringForward();
    }

    private static int ReadSiblingCount(Visio.Shape shape)
    {
        Visio.Shape? parent = null;
        Visio.Shapes? siblings = null;
        try
        {
            parent = shape.ContainingShape;
            siblings = parent is null ? shape.ContainingPage.Shapes : parent.Shapes;
            return siblings.Count;
        }
        finally
        {
            if (siblings is not null) Marshal.FinalReleaseComObject(siblings);
            if (parent is not null) Marshal.FinalReleaseComObject(parent);
        }
    }

    private void RewriteChildSemanticIds(
        Visio.Shape parent,
        string rootSemanticId,
        IDictionary<string, string> mapping)
    {
        var children = parent.Shapes;
        try
        {
            for (var index = 1; index <= children.Count; index++)
            {
                var child = children[index];
                try
                {
                    using var handle = new VisioShapeHandle(child);
                    var oldSemanticId = handle.UserCodexId;
                    if (!string.IsNullOrWhiteSpace(oldSemanticId))
                    {
                        var newSemanticId = $"{rootSemanticId}__child_{mapping.Count + 1}";
                        _repository.Register(handle, newSemanticId);
                        mapping[oldSemanticId] = newSemanticId;
                    }
                    RewriteChildSemanticIds(child, rootSemanticId, mapping);
                    child = null!;
                }
                finally
                {
                    if (child is not null) Marshal.FinalReleaseComObject(child);
                }
            }
        }
        finally
        {
            Marshal.FinalReleaseComObject(children);
        }
    }

    private static BridgeCommandException Invalid(string message) =>
        new(BridgeErrorCodes.InvalidArgument, message);
}
