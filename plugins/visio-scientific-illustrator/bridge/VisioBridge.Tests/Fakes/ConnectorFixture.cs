using VisioBridge.Contracts;
using VisioBridge.Core.Abstractions;
using VisioBridge.Core.Shapes;
using System.Runtime.InteropServices;

namespace VisioBridge.Tests.Fakes;

internal sealed class ConnectorFixture
{
    private ConnectorFixture(ConnectorService service, FakeConnectorAdapter adapter)
    {
        Service = service;
        Adapter = adapter;
    }

    public ConnectorService Service { get; }

    public FakeConnectorAdapter Adapter { get; }

    public static ConnectorService CreateWithoutShapes()
    {
        var accessor = FakeShapeAccessor.WithShapes();
        return new ConnectorService(
            new ShapeRepository(accessor),
            new FakeConnectorAdapter(accessor));
    }

    public static ConnectorFixture CreateWithShapes(params string[] semanticIds)
    {
        var shapes = semanticIds
            .Select((semanticId, index) => new FakeShape(index + 1, semanticId)
            {
                NameU = ShapeRepository.ToUniversalName(semanticId),
            })
            .ToArray();
        var accessor = FakeShapeAccessor.WithShapes(shapes);
        var adapter = new FakeConnectorAdapter(accessor);
        return new ConnectorFixture(
            new ConnectorService(new ShapeRepository(accessor), adapter),
            adapter);
    }
}

internal sealed class FakeConnectorAdapter : IConnectorAdapter
{
    private FakeShape? _connector;

    public FakeConnectorAdapter(FakeShapeAccessor accessor)
    {
        ArgumentNullException.ThrowIfNull(accessor);
    }

    public bool GlueCalled { get; private set; }

    public string? Routing { get; private set; }

    public IReadOnlyList<PixelPoint>? Waypoints { get; private set; }

    public string? Label { get; private set; }

    public int? SourceConnectionPoint { get; private set; }

    public int? TargetConnectionPoint { get; private set; }

    public ConnectorAnchor? SourceAnchor { get; private set; }

    public ConnectorAnchor? TargetAnchor { get; private set; }

    public string? ConnectorSemanticId { get; private set; }

    public ConnectorAnchor? CreatedSourceAnchor { get; private set; }

    public ConnectorAnchor? CreatedTargetAnchor { get; private set; }

    public string? CreatedConnectorSemanticId { get; private set; }

    public string? JumpStyle { get; private set; }

    public double? LabelPosition { get; private set; }

    public IReadOnlyList<string>? AvoidShapeIds { get; private set; }

    public COMException? CreateError { get; set; }

    public IShapeHandle Create(
        IShapeHandle source,
        IShapeHandle target,
        string routing,
        IReadOnlyList<PixelPoint>? waypoints,
        IReadOnlyList<IShapeHandle> avoidShapes,
        ConnectorAnchor sourceAnchor,
        ConnectorAnchor targetAnchor,
        string connectorSemanticId)
    {
        if (CreateError is not null) throw CreateError;
        Routing = routing;
        Waypoints = waypoints;
        AvoidShapeIds = avoidShapes.Cast<FakeShape>().Select(shape => shape.UserCodexId!).ToArray();
        SourceAnchor = sourceAnchor;
        TargetAnchor = targetAnchor;
        ConnectorSemanticId = connectorSemanticId;
        CreatedSourceAnchor = sourceAnchor;
        CreatedTargetAnchor = targetAnchor;
        CreatedConnectorSemanticId = connectorSemanticId;
        _connector = new FakeShape(100, null);
        return _connector;
    }

    public void Glue(
        IShapeHandle connector,
        IShapeHandle source,
        IShapeHandle target,
        ConnectorAnchor sourceAnchor,
        ConnectorAnchor targetAnchor,
        string connectorSemanticId)
    {
        GlueCalled = true;
        SourceAnchor = sourceAnchor;
        TargetAnchor = targetAnchor;
        ConnectorSemanticId = connectorSemanticId;
        SourceConnectionPoint = sourceAnchor.Row;
        TargetConnectionPoint = targetAnchor.Row;
    }

    public void SetLabel(IShapeHandle connector, string? label, double? labelPosition)
    {
        Label = label;
        LabelPosition = labelPosition;
    }

    public void ApplyRoutingOptions(IShapeHandle connector, string? jumpStyle)
    {
        JumpStyle = jumpStyle;
    }

    public void ApplyStyle(IShapeHandle connector, ConnectorStyle? style)
    {
    }

    public int CountVerifiedEndpointConnections(
        IShapeHandle connector,
        IShapeHandle source,
        IShapeHandle target) => GlueCalled ? 2 : 0;

    public void Delete(IShapeHandle connector)
    {
    }
}
