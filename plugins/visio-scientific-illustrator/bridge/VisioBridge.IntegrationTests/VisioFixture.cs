using VisioBridge.Contracts;
using VisioBridge.Core.Abstractions;
using VisioBridge.Core.Commands;
using VisioBridge.Core.Session;

namespace VisioBridge.IntegrationTests;

public sealed class VisioFixture : IDisposable
{
    private readonly VisioSession _session;
    private bool _disposed;

    private VisioFixture(VisioSession session)
    {
        _session = session;
        Router = new CommandRouter(session);
    }

    public CommandRouter Router { get; }

    public int OwnedProcessId => _session.OwnedProcessId
        ?? throw new InvalidOperationException("Fixture 没有 owned Visio PID。");

    public ShapeMutationResult AddShape(AddShapeCommand command) => _session.AddShape(command);

    public ConnectorMutationResult AddConnector(AddConnectorCommand command) =>
        _session.AddConnector(command);

    public PageInspectionResult Inspect(int maxShapes = 500) =>
        _session.Inspect(maxShapes);

    public ShapeSnapshot UpdateShape(UpdateShapeCommand command) =>
        _session.UpdateShape(command);

    public DeleteResult DeleteShape(DeleteShapeCommand command) =>
        _session.DeleteShape(command);

    public void Undo() => _session.UndoLastForIntegrationTest();

    public RasterSettings ReadRasterSettings() =>
        _session.ReadRasterSettingsForIntegrationTest();

    public PreviewResult ExportPreview(int widthPx) =>
        _session.ExportPreview(widthPx);

    public SaveResult Save(string outputPath, bool overwrite = false) =>
        _session.Save(new SaveCommand(outputPath, overwrite));

    public GroupMutationResult GroupShapes(GroupShapesCommand command) =>
        _session.GroupShapes(command);

    public UngroupMutationResult UngroupShape(UngroupShapeCommand command) =>
        _session.UngroupShape(command);

    public ContainerMutationResult AddToContainer(AddToContainerCommand command) =>
        _session.AddToContainer(command);

    public static VisioFixture WithOneShape()
    {
        var fixture = StartVisible();
        fixture.AddShape(new AddShapeCommand(
            "preview-shape",
            "rectangle",
            new PixelBox(200, 200, 400, 200),
            "Preview"));
        return fixture;
    }

    public SessionStatus GetStatus() => _session.GetStatus();

    public static VisioFixture StartVisible()
    {
        var pluginRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            ".."));
        var session = new VisioSession(pluginRoot);
        try
        {
            session.LaunchNew("Figure", 1600, 1000, 96, true);
            return new VisioFixture(session);
        }
        catch
        {
            session.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_session.OwnedProcessId is not null)
        {
            _session.Shutdown(true, "discard");
        }
        _session.Dispose();
    }
}
