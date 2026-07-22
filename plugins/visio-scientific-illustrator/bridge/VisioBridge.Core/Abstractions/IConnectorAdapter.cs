using VisioBridge.Contracts;

namespace VisioBridge.Core.Abstractions;

public interface IConnectorAdapter
{
    IShapeHandle Create(IShapeHandle source, IShapeHandle target);

    void Glue(IShapeHandle connector, IShapeHandle source, IShapeHandle target);

    void ApplyStyle(IShapeHandle connector, ConnectorStyle? style);

    int CountVerifiedEndpointConnections(
        IShapeHandle connector,
        IShapeHandle source,
        IShapeHandle target);

    void Delete(IShapeHandle connector);
}
