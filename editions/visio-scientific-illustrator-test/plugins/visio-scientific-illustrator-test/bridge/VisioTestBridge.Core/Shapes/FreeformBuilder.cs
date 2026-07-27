using System.Globalization;
using System.Runtime.InteropServices;
using VisioTestBridge.Contracts;
using VisioTestBridge.Core.Geometry;
using VisioTestBridge.Core.Inspection;
using Visio = Microsoft.Office.Interop.Visio;

namespace VisioTestBridge.Core.Shapes;

internal enum GeometryRowKind
{
    RelMoveTo,
    RelLineTo,
    RelCubBezTo,
    RelEllipticalArcTo,
}

internal sealed record CompiledGeometryRow(
    GeometryRowKind Kind,
    double X,
    double Y,
    double? A = null,
    double? B = null,
    double? C = null,
    double? D = null);

internal sealed record CompiledGeometrySection(
    IReadOnlyList<CompiledGeometryRow> Rows,
    bool NoFill);

internal sealed record CompiledPathGeometry(
    IReadOnlyList<CompiledGeometrySection> Sections)
{
    public int RowCount => Sections.Sum(section => section.Rows.Count);
}

internal static class PathGeometryCompiler
{
    private const int MaximumSections = 32;
    private const int MaximumCommands = 2000;

    public static CompiledPathGeometry Compile(AddPathCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateBounds(command.Bounds);
        if (string.IsNullOrWhiteSpace(command.SemanticId))
            throw Invalid("semantic_id 不能为空。");
        if (command.Sections is null || command.Sections.Count is < 1 or > MaximumSections)
            throw Invalid($"路径必须包含 1–{MaximumSections} 个 Geometry Section。");
        if (command.Sections.Sum(section => section?.Commands?.Count ?? 0) > MaximumCommands)
            throw Invalid($"单路径最多允许 {MaximumCommands} 条命令。");

        var sections = command.Sections
            .Select((section, index) => CompileSection(section, command.Bounds, index))
            .ToArray();
        return new CompiledPathGeometry(sections);
    }

    private static CompiledGeometrySection CompileSection(
        PathSection? section,
        PixelBox bounds,
        int sectionIndex)
    {
        if (section?.Commands is null || section.Commands.Count == 0)
            throw Invalid($"Geometry Section {sectionIndex} 至少需要一条命令。");
        if (!string.Equals(section.Commands[0].Op, "M", StringComparison.Ordinal))
            throw Invalid($"Geometry Section {sectionIndex} 的首命令必须是 M。");

        var terminalClose = string.Equals(section.Commands[^1].Op, "Z", StringComparison.Ordinal);
        if (section.Filled && !section.Closed && !terminalClose)
            throw Invalid($"Geometry Section {sectionIndex} 的填充路径必须闭合。");

        var rows = new List<CompiledGeometryRow>(section.Commands.Count + 1);
        LocalPoint? current = null;
        LocalPoint? subpathStart = null;
        foreach (var (pathCommand, commandIndex) in section.Commands.Select((value, index) => (value, index)))
        {
            if (pathCommand is null)
                throw Invalid($"Geometry Section {sectionIndex} 的命令 {commandIndex} 不能为空。");
            var op = pathCommand.Op;
            switch (op)
            {
                case "M":
                {
                    var point = RequiredPoint(pathCommand.X, pathCommand.Y, bounds, sectionIndex, commandIndex, "终点");
                    rows.Add(Row(GeometryRowKind.RelMoveTo, point, bounds));
                    current = point;
                    subpathStart = point;
                    break;
                }
                case "L":
                {
                    RequireCurrent(current, sectionIndex, commandIndex);
                    var point = RequiredPoint(pathCommand.X, pathCommand.Y, bounds, sectionIndex, commandIndex, "终点");
                    rows.Add(Row(GeometryRowKind.RelLineTo, point, bounds));
                    current = point;
                    break;
                }
                case "C":
                {
                    RequireCurrent(current, sectionIndex, commandIndex);
                    var end = RequiredPoint(pathCommand.X, pathCommand.Y, bounds, sectionIndex, commandIndex, "终点");
                    var control1 = RequiredPoint(pathCommand.X1, pathCommand.Y1, bounds, sectionIndex, commandIndex, "第一控制点");
                    var control2 = RequiredPoint(pathCommand.X2, pathCommand.Y2, bounds, sectionIndex, commandIndex, "第二控制点");
                    rows.Add(new CompiledGeometryRow(
                        GeometryRowKind.RelCubBezTo,
                        NormalizeX(end.X, bounds),
                        NormalizeY(end.Y, bounds),
                        NormalizeX(control1.X, bounds),
                        NormalizeY(control1.Y, bounds),
                        NormalizeX(control2.X, bounds),
                        NormalizeY(control2.Y, bounds)));
                    current = end;
                    break;
                }
                case "A":
                {
                    RequireCurrent(current, sectionIndex, commandIndex);
                    var end = RequiredPoint(pathCommand.X, pathCommand.Y, bounds, sectionIndex, commandIndex, "终点");
                    rows.Add(CompileArc(current!.Value, end, pathCommand, bounds, sectionIndex, commandIndex));
                    current = end;
                    break;
                }
                case "Z":
                {
                    RequireCurrent(current, sectionIndex, commandIndex);
                    if (subpathStart is null)
                        throw Invalid($"Geometry Section {sectionIndex} 的命令 {commandIndex} 没有可闭合子路径。");
                    rows.Add(Row(GeometryRowKind.RelLineTo, subpathStart.Value, bounds));
                    current = subpathStart;
                    break;
                }
                default:
                    throw Invalid($"Geometry Section {sectionIndex} 的命令 {commandIndex} 不支持操作: {op}");
            }
        }

        if (section.Closed && !terminalClose)
        {
            if (subpathStart is null) throw Invalid($"Geometry Section {sectionIndex} 没有可闭合子路径。");
            rows.Add(Row(GeometryRowKind.RelLineTo, subpathStart.Value, bounds));
        }
        return new CompiledGeometrySection(rows, !section.Filled);
    }

    private static CompiledGeometryRow CompileArc(
        LocalPoint start,
        LocalPoint end,
        ScenePathCommand command,
        PixelBox bounds,
        int sectionIndex,
        int commandIndex)
    {
        var rx = RequiredPositive(command.Rx, sectionIndex, commandIndex, "rx");
        var ry = RequiredPositive(command.Ry, sectionIndex, commandIndex, "ry");
        var rotation = command.RotationDeg ?? 0;
        if (!double.IsFinite(rotation) || rotation is < -360 or > 360)
            throw Invalid($"Geometry Section {sectionIndex} 的命令 {commandIndex} rotation_deg 必须在 -360–360 范围内。");
        if (command.LargeArc is null || command.Sweep is null)
            throw Invalid($"Geometry Section {sectionIndex} 的命令 {commandIndex} 必须提供 large_arc 和 sweep。");
        if (NearlyEqual(start.X, end.X) && NearlyEqual(start.Y, end.Y))
            throw Invalid($"Geometry Section {sectionIndex} 的命令 {commandIndex} 弧线起点和终点不能相同。");

        var radians = rotation * Math.PI / 180d;
        var cos = Math.Cos(radians);
        var sin = Math.Sin(radians);
        var dx = (start.X - end.X) / 2d;
        var dy = (start.Y - end.Y) / 2d;
        var xPrime = cos * dx + sin * dy;
        var yPrime = -sin * dx + cos * dy;

        var radiusScale = xPrime * xPrime / (rx * rx) + yPrime * yPrime / (ry * ry);
        if (radiusScale > 1)
        {
            var scale = Math.Sqrt(radiusScale);
            rx *= scale;
            ry *= scale;
        }

        var rxSquared = rx * rx;
        var rySquared = ry * ry;
        var denominator = rxSquared * yPrime * yPrime + rySquared * xPrime * xPrime;
        if (denominator <= double.Epsilon)
            throw Invalid($"Geometry Section {sectionIndex} 的命令 {commandIndex} 无法构造椭圆弧。");
        var numerator = Math.Max(0, rxSquared * rySquared - denominator);
        var sign = command.LargeArc.Value == command.Sweep.Value ? -1d : 1d;
        var coefficient = sign * Math.Sqrt(numerator / denominator);
        var centerPrimeX = coefficient * rx * yPrime / ry;
        var centerPrimeY = coefficient * -ry * xPrime / rx;
        var centerX = cos * centerPrimeX - sin * centerPrimeY + (start.X + end.X) / 2d;
        var centerY = sin * centerPrimeX + cos * centerPrimeY + (start.Y + end.Y) / 2d;

        var startVectorX = (xPrime - centerPrimeX) / rx;
        var startVectorY = (yPrime - centerPrimeY) / ry;
        var endVectorX = (-xPrime - centerPrimeX) / rx;
        var endVectorY = (-yPrime - centerPrimeY) / ry;
        var startAngle = Math.Atan2(startVectorY, startVectorX);
        var deltaAngle = VectorAngle(startVectorX, startVectorY, endVectorX, endVectorY);
        if (!command.Sweep.Value && deltaAngle > 0) deltaAngle -= 2 * Math.PI;
        if (command.Sweep.Value && deltaAngle < 0) deltaAngle += 2 * Math.PI;
        var middleAngle = startAngle + deltaAngle / 2d;
        var controlX = centerX
            + cos * rx * Math.Cos(middleAngle)
            - sin * ry * Math.Sin(middleAngle);
        var controlY = centerY
            + sin * rx * Math.Cos(middleAngle)
            + cos * ry * Math.Sin(middleAngle);

        return new CompiledGeometryRow(
            GeometryRowKind.RelEllipticalArcTo,
            NormalizeX(end.X, bounds),
            NormalizeY(end.Y, bounds),
            NormalizeX(controlX, bounds),
            NormalizeY(controlY, bounds),
            -rotation,
            rx / ry);
    }

    private static double VectorAngle(double ux, double uy, double vx, double vy) =>
        Math.Atan2(ux * vy - uy * vx, ux * vx + uy * vy);

    private static CompiledGeometryRow Row(GeometryRowKind kind, LocalPoint point, PixelBox bounds) =>
        new(kind, NormalizeX(point.X, bounds), NormalizeY(point.Y, bounds));

    private static LocalPoint RequiredPoint(
        double? x,
        double? y,
        PixelBox bounds,
        int sectionIndex,
        int commandIndex,
        string name)
    {
        if (x is null || y is null || !double.IsFinite(x.Value) || !double.IsFinite(y.Value)
            || x.Value < 0 || y.Value < 0 || x.Value > bounds.Width || y.Value > bounds.Height)
        {
            throw Invalid($"Geometry Section {sectionIndex} 的命令 {commandIndex} {name}必须位于局部 bounds 内。");
        }
        return new LocalPoint(x.Value, y.Value);
    }

    private static double RequiredPositive(double? value, int sectionIndex, int commandIndex, string name)
    {
        if (value is null || !double.IsFinite(value.Value) || value.Value <= 0)
            throw Invalid($"Geometry Section {sectionIndex} 的命令 {commandIndex} {name} 必须是正有限数值。");
        return value.Value;
    }

    private static void RequireCurrent(LocalPoint? point, int sectionIndex, int commandIndex)
    {
        if (point is null)
            throw Invalid($"Geometry Section {sectionIndex} 的命令 {commandIndex} 前缺少 M 命令。");
    }

    private static void ValidateBounds(PixelBox bounds)
    {
        if (!double.IsFinite(bounds.X) || !double.IsFinite(bounds.Y)
            || !double.IsFinite(bounds.Width) || !double.IsFinite(bounds.Height)
            || bounds.Width <= 0 || bounds.Height <= 0)
        {
            throw Invalid("路径 bounds 必须是正有限矩形。");
        }
    }

    private static double NormalizeX(double x, PixelBox bounds) => x / bounds.Width;
    private static double NormalizeY(double y, PixelBox bounds) => 1d - y / bounds.Height;
    private static bool NearlyEqual(double left, double right) => Math.Abs(left - right) <= 1e-9;

    private static BridgeCommandException Invalid(string message) =>
        new(BridgeErrorCodes.InvalidArgument, message);

    private readonly record struct LocalPoint(double X, double Y);
}

internal sealed class FreeformBuilder
{
    private const short FirstGeometrySection = (short)Visio.VisSectionIndices.visSectionFirstComponent;
    private const short MaximumGeometrySectionToRemove = FirstGeometrySection + 255;
    private readonly Visio.Page _page;
    private readonly GeometryConverter _geometry;
    private readonly ShapeRepository _repository;
    private readonly PageInspector _inspector;

    public FreeformBuilder(
        Visio.Page page,
        GeometryConverter geometry,
        ShapeRepository repository,
        PageInspector inspector)
    {
        _page = page;
        _geometry = geometry;
        _repository = repository;
        _inspector = inspector;
    }

    public PathMutationResult Add(AddPathCommand command)
    {
        var compiled = PathGeometryCompiler.Compile(command);
        var box = _geometry.ToVisio(command.Bounds);
        Visio.Shape? shape = _page.DrawRectangle(
            box.PinXInches - box.WidthInches / 2d,
            box.PinYInches - box.HeightInches / 2d,
            box.PinXInches + box.WidthInches / 2d,
            box.PinYInches + box.HeightInches / 2d);
        try
        {
            ShapeFactory.SetGeometry(shape, box);
            ReplaceGeometry(shape, compiled);
            StyleMapper.Apply(shape, command.Style);
            using var handle = new VisioShapeHandle(shape);
            shape = null;
            _repository.Register(handle, command.SemanticId);
            return Result(handle.Shape, command.SemanticId, compiled);
        }
        catch
        {
            if (shape is not null)
            {
                shape.Delete();
                Marshal.FinalReleaseComObject(shape);
            }
            throw;
        }
    }

    public PathMutationResult Update(UpdatePathCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Bounds is null && command.Sections is null && command.Style is null)
            throw new BridgeCommandException(BridgeErrorCodes.InvalidArgument, "update_path 至少需要 bounds、sections 或 style 之一。");

        using var handle = _repository.Resolve(command.SemanticId);
        var shape = ((VisioShapeHandle)handle).Shape;
        CompiledPathGeometry? compiled = null;
        if (command.Sections is not null)
        {
            var localBounds = command.Bounds ?? ReadExactBounds(shape);
            compiled = PathGeometryCompiler.Compile(new AddPathCommand(
                command.SemanticId,
                localBounds,
                command.Sections,
                command.Style));
        }
        if (command.Bounds is not null)
            ShapeFactory.SetGeometry(shape, _geometry.ToVisio(command.Bounds));
        if (compiled is not null) ReplaceGeometry(shape, compiled);
        if (command.Style is not null) StyleMapper.Apply(shape, command.Style);

        return compiled is not null
            ? Result(shape, command.SemanticId, compiled)
            : ResultFromShape(shape, command.SemanticId);
    }

    private PixelBox ReadExactBounds(Visio.Shape shape)
    {
        var pinX = ReadInches(shape, "PinX");
        var pinY = ReadInches(shape, "PinY");
        var width = ReadInches(shape, "Width");
        var height = ReadInches(shape, "Height");
        return _geometry.ToPixelsAllowingOverflow(new VisioBox(pinX, pinY, width, height));
    }

    private PathMutationResult Result(
        Visio.Shape shape,
        string semanticId,
        CompiledPathGeometry compiled) => new(
            semanticId,
            shape.ID,
            shape.NameU,
            _inspector.ReadBounds(shape),
            compiled.Sections.Count,
            compiled.RowCount);

    private PathMutationResult ResultFromShape(Visio.Shape shape, string semanticId)
    {
        var sectionCount = 0;
        var rowCount = 0;
        for (short section = FirstGeometrySection; section <= MaximumGeometrySectionToRemove; section++)
        {
            if (!SectionExists(shape, section)) continue;
            sectionCount++;
            rowCount += Math.Max(0, shape.RowCount[section] - 1);
        }
        return new PathMutationResult(
            semanticId,
            shape.ID,
            shape.NameU,
            _inspector.ReadBounds(shape),
            sectionCount,
            rowCount);
    }

    private static void ReplaceGeometry(Visio.Shape shape, CompiledPathGeometry geometry)
    {
        for (short section = MaximumGeometrySectionToRemove; section >= FirstGeometrySection; section--)
        {
            if (SectionExists(shape, section)) shape.DeleteSection(section);
        }

        for (var sectionIndex = 0; sectionIndex < geometry.Sections.Count; sectionIndex++)
        {
            var section = checked((short)(FirstGeometrySection + sectionIndex));
            shape.AddSection(section);
            shape.AddRow(
                section,
                (short)Visio.VisRowIndices.visRowComponent,
                (short)Visio.VisRowTags.visTagComponent);
            for (var rowIndex = 0; rowIndex < geometry.Sections[sectionIndex].Rows.Count; rowIndex++)
                WriteRow(shape, section, geometry.Sections[sectionIndex].Rows[rowIndex], rowIndex == 0);
            WriteSectionProperties(shape, section, geometry.Sections[sectionIndex]);
        }
    }

    private static void WriteSectionProperties(
        Visio.Shape shape,
        short section,
        CompiledGeometrySection geometry)
    {
        SetCell(shape, section, (short)Visio.VisRowIndices.visRowComponent,
            (short)Visio.VisCellIndices.visCompNoFill, geometry.NoFill ? "1" : "0");
        SetCell(shape, section, (short)Visio.VisRowIndices.visRowComponent,
            (short)Visio.VisCellIndices.visCompNoLine, "0");
        SetCell(shape, section, (short)Visio.VisRowIndices.visRowComponent,
            (short)Visio.VisCellIndices.visCompNoShow, "0");
    }

    private static void WriteRow(
        Visio.Shape shape,
        short section,
        CompiledGeometryRow row,
        bool firstRow)
    {
        var rowIndex = shape.AddRow(
            section,
            firstRow
                ? (short)Visio.VisRowIndices.visRowVertex
                : (short)Visio.VisRowIndices.visRowLast,
            RowTag(row.Kind));
        SetCell(shape, section, rowIndex, (short)Visio.VisCellIndices.visX, Number(row.X));
        SetCell(shape, section, rowIndex, (short)Visio.VisCellIndices.visY, Number(row.Y));
        if (row.A is not null) SetCell(shape, section, rowIndex, 2, Number(row.A.Value));
        if (row.B is not null) SetCell(shape, section, rowIndex, 3, Number(row.B.Value));
        if (row.C is not null)
        {
            var formula = row.Kind == GeometryRowKind.RelEllipticalArcTo
                ? $"{Number(row.C.Value)} deg"
                : Number(row.C.Value);
            SetCell(shape, section, rowIndex, 4, formula);
        }
        if (row.D is not null) SetCell(shape, section, rowIndex, 5, Number(row.D.Value));
    }

    private static short RowTag(GeometryRowKind kind) => kind switch
    {
        GeometryRowKind.RelMoveTo => (short)Visio.VisRowTags.visTagRelMoveTo,
        GeometryRowKind.RelLineTo => (short)Visio.VisRowTags.visTagRelLineTo,
        GeometryRowKind.RelCubBezTo => (short)Visio.VisRowTags.visTagRelCubBezTo,
        GeometryRowKind.RelEllipticalArcTo => (short)Visio.VisRowTags.visTagRelEllipticalArcTo,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static bool SectionExists(Visio.Shape shape, short section) =>
        shape.SectionExists[section, (short)Visio.VisExistsFlags.visExistsAnywhere] != 0;

    private static void SetCell(
        Visio.Shape shape,
        short section,
        short row,
        short cellIndex,
        string formula)
    {
        var cell = shape.CellsSRC[section, row, cellIndex];
        try
        {
            cell.FormulaU = formula;
        }
        finally
        {
            Marshal.FinalReleaseComObject(cell);
        }
    }

    private static double ReadInches(Visio.Shape shape, string cellName)
    {
        var cell = shape.CellsU[cellName];
        try
        {
            return cell.ResultIU;
        }
        finally
        {
            Marshal.FinalReleaseComObject(cell);
        }
    }

    private static string Number(double value) =>
        value.ToString("0.###############", CultureInfo.InvariantCulture);
}
