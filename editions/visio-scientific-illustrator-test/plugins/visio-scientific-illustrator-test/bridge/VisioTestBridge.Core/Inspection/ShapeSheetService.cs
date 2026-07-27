using System.Runtime.InteropServices;
using VisioTestBridge.Contracts;
using VisioTestBridge.Core.Shapes;
using VisioTestBridge.Core.Styling;
using Visio = Microsoft.Office.Interop.Visio;

namespace VisioTestBridge.Core.Inspection;

internal sealed class ShapeSheetService
{
    private const short FirstGeometrySection = (short)Visio.VisSectionIndices.visSectionFirstComponent;
    private readonly ShapeRepository _repository;

    public ShapeSheetService(ShapeRepository repository)
    {
        _repository = repository;
    }

    public ShapeSheetCellsResult GetCells(GetCellsCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateCells(command.Cells);
        using var handle = _repository.Resolve(command.SemanticId);
        var shape = ((VisioShapeHandle)handle).Shape;
        var cells = command.Cells.Select(cellName => ReadCell(shape, cellName)).ToArray();
        return new ShapeSheetCellsResult(command.SemanticId, cells);
    }

    public ShapeSheetCellsResult SetCells(SetCellsCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Writes is null || command.Writes.Count is < 1 or > 128
            || command.Writes.Select(item => item.Cell).Distinct(StringComparer.Ordinal).Count() != command.Writes.Count)
            throw Invalid("writes 必须包含 1–128 个且 cell 不重复的写入。");
        using var handle = _repository.Resolve(command.SemanticId);
        var shape = ((VisioShapeHandle)handle).Shape;
        var formulas = command.Writes.ToDictionary(
            write => write.Cell,
            write => ControlledShapeSheet.Compile(write.Cell, write.Value),
            StringComparer.Ordinal);
        ShapeSheetWriter.Apply(shape, formulas);
        return new ShapeSheetCellsResult(
            command.SemanticId,
            command.Writes.Select(write => ReadCell(shape, write.Cell)).ToArray());
    }

    public ConnectionPointMutationResult AddConnectionPoint(AddConnectionPointCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        using var handle = _repository.Resolve(command.SemanticId);
        ConnectionPointService.AddNamedPoint(
            ((VisioShapeHandle)handle).Shape,
            command.Name,
            command.X,
            command.Y,
            command.DirectionX,
            command.DirectionY);
        return new ConnectionPointMutationResult(command.SemanticId, command.Name, command.X, command.Y);
    }

    public GeometryInspectionResult GetGeometry(GetGeometryCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        using var handle = _repository.Resolve(command.SemanticId);
        var shape = ((VisioShapeHandle)handle).Shape;
        var sections = new List<GeometrySectionSnapshot>();
        for (short section = FirstGeometrySection; section < FirstGeometrySection + 256; section++)
        {
            if (shape.SectionExists[section, (short)Visio.VisExistsFlags.visExistsAnywhere] == 0) continue;
            var count = shape.RowCount[section];
            var rows = command.IncludeRows ? ReadRows(shape, section, count) : [];
            sections.Add(new GeometrySectionSnapshot(section - FirstGeometrySection, count, rows));
        }
        return new GeometryInspectionResult(command.SemanticId, sections);
    }

    private static IReadOnlyList<GeometryRowSnapshot> ReadRows(Visio.Shape shape, short section, short count)
    {
        var rows = new List<GeometryRowSnapshot>();
        for (short row = 0; row < count; row++)
        {
            var cells = new Dictionary<string, string>(StringComparer.Ordinal);
            var names = new[] { "X", "Y", "A", "B", "C", "D" };
            for (short cellIndex = 0; cellIndex < names.Length; cellIndex++)
            {
                Visio.Cell? cell = null;
                try
                {
                    cell = shape.CellsSRC[section, row, cellIndex];
                    cells[names[cellIndex]] = cell.FormulaU;
                }
                catch (COMException)
                {
                    // 不同 Geometry 行拥有不同的合法列；缺失列不属于错误。
                }
                finally
                {
                    if (cell is not null) Marshal.FinalReleaseComObject(cell);
                }
            }
            rows.Add(new GeometryRowSnapshot(row, shape.RowType[section, row], cells));
        }
        return rows;
    }

    private static ShapeSheetCellSnapshot ReadCell(Visio.Shape shape, string cellName)
    {
        if (!ControlledShapeSheet.IsAllowed(cellName)) throw Invalid($"不允许访问 ShapeSheet 单元格：{cellName}");
        if (shape.CellExistsU[cellName, (short)Visio.VisExistsFlags.visExistsAnywhere] == 0)
            throw new BridgeCommandException(BridgeErrorCodes.CellNotAvailable, $"Shape 缺少单元格：{cellName}");
        var cell = shape.CellsU[cellName];
        try
        {
            return new ShapeSheetCellSnapshot(
                cellName,
                cell.FormulaU,
                cell.ResultStrU[(object)Visio.VisUnitCodes.visUnitsString]);
        }
        finally { Marshal.FinalReleaseComObject(cell); }
    }

    private static void ValidateCells(IReadOnlyList<string> cells)
    {
        if (cells is null || cells.Count is < 1 or > 128
            || cells.Any(cell => !ControlledShapeSheet.IsAllowed(cell))
            || cells.Distinct(StringComparer.Ordinal).Count() != cells.Count)
            throw Invalid("cells 必须包含 1–128 个允许且不重复的通用单元格名称。");
    }

    private static BridgeCommandException Invalid(string message) =>
        new(BridgeErrorCodes.InvalidArgument, message);
}
