using System.Runtime.InteropServices;
using VisioBridge.Contracts;
using VisioBridge.Core.Geometry;
using VisioBridge.Core.Inspection;
using VisioBridge.Core.Text;
using Visio = Microsoft.Office.Interop.Visio;

namespace VisioBridge.Core.Shapes;

internal sealed class ShapeFactory
{
    private static readonly HashSet<string> ImageExtensions = new(
        [".png", ".jpg", ".jpeg", ".svg", ".emf"],
        StringComparer.OrdinalIgnoreCase);

    private readonly Visio.Page _page;
    private readonly GeometryConverter _geometry;
    private readonly ShapeRepository _repository;
    private readonly MasterCatalog _masterCatalog;
    private readonly PageInspector _inspector;

    public ShapeFactory(
        Visio.Page page,
        GeometryConverter geometry,
        ShapeRepository repository,
        MasterCatalog masterCatalog,
        PageInspector inspector)
    {
        _page = page;
        _geometry = geometry;
        _repository = repository;
        _masterCatalog = masterCatalog;
        _inspector = inspector;
    }

    public ShapeMutationResult Add(AddShapeCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var box = _geometry.ToVisio(command.Bounds);
        Visio.Shape? shape = command.Shape switch
        {
            "rectangle" => DrawRectangle(box),
            "ellipse" => DrawEllipse(box),
            "text" => DrawRectangle(box),
            _ when MasterCatalog.IsKnown(command.Shape) =>
                _masterCatalog.Drop(_page, command.Shape, box.PinXInches, box.PinYInches),
            _ => throw BridgeCommandException.Unsupported($"不支持基础 Shape: {command.Shape}"),
        };

        try
        {
            SetGeometry(shape, box);
            if (command.Label is not null && command.RichText is not null)
                throw new BridgeCommandException(BridgeErrorCodes.InvalidArgument, "label 与 rich_text 不能同时提供。");
            if (command.RichText is not null) RichTextFormatter.Apply(shape, command.RichText);
            else if (command.Label is not null) shape.Text = command.Label;
            if (command.Shape != "container")
            {
                StyleMapper.ApplyTextDefaults(shape, command.Shape == "text");
            }
            StyleMapper.Apply(shape, command.Style);
            using var handle = new VisioShapeHandle(shape);
            shape = null;
            _repository.Register(handle, command.SemanticId);
            var result = new ShapeMutationResult(
                command.SemanticId,
                handle.Id,
                handle.NameU,
                _inspector.ReadBounds(handle.Shape),
                command.Shape);
            return result;
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

    public ShapeMutationResult AddImage(AddImageCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!Path.IsPathFullyQualified(command.FilePath)
            || !File.Exists(command.FilePath)
            || !ImageExtensions.Contains(Path.GetExtension(command.FilePath)))
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.InvalidArgument,
                "file_path 必须是已存在的绝对 PNG、JPEG、SVG 或 EMF 路径。");
        }

        var box = _geometry.ToVisio(command.Bounds);
        Visio.Shape? shape = _page.Import(command.FilePath);
        try
        {
            SetGeometry(shape, box);
            using var handle = new VisioShapeHandle(shape);
            shape = null;
            _repository.Register(handle, command.SemanticId);
            var extension = Path.GetExtension(command.FilePath);
            var result = new ShapeMutationResult(
                command.SemanticId,
                handle.Id,
                handle.NameU,
                _inspector.ReadBounds(handle.Shape),
                "image",
                extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
                    || extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
                    || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase));
            return result;
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

    private Visio.Shape DrawRectangle(VisioBox box) => _page.DrawRectangle(
        box.PinXInches - box.WidthInches / 2d,
        box.PinYInches - box.HeightInches / 2d,
        box.PinXInches + box.WidthInches / 2d,
        box.PinYInches + box.HeightInches / 2d);

    private Visio.Shape DrawEllipse(VisioBox box) => _page.DrawOval(
        box.PinXInches - box.WidthInches / 2d,
        box.PinYInches - box.HeightInches / 2d,
        box.PinXInches + box.WidthInches / 2d,
        box.PinYInches + box.HeightInches / 2d);

    internal static void SetGeometry(Visio.Shape shape, VisioBox box)
    {
        StyleMapper.SetResultInches(shape, "PinX", box.PinXInches);
        StyleMapper.SetResultInches(shape, "PinY", box.PinYInches);
        StyleMapper.SetResultInches(shape, "Width", box.WidthInches);
        StyleMapper.SetResultInches(shape, "Height", box.HeightInches);
    }
}
