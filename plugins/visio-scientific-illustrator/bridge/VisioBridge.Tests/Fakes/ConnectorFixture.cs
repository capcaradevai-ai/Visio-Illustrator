using VisioBridge.Contracts;
using VisioBridge.Core.Abstractions;
using VisioBridge.Core.Shapes;

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

    public IShapeHandle Create(IShapeHandle source, IShapeHandle target)
    {
        _connector = new FakeShape(100, null);
        return _connector;
    }

    public void Glue(IShapeHandle connector, IShapeHandle source, IShapeHandle target)
    {
        GlueCalled = true;
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
