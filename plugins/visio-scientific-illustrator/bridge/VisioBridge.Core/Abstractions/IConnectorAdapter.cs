using VisioBridge.Contracts;

namespace VisioBridge.Core.Abstractions;

public interface IConnectorAdapter
{
    IShapeHandle Create(
        IShapeHandle source,
        IShapeHandle target,
        string routing,
        IReadOnlyList<PixelPoint>? waypoints);

    void Glue(
        IShapeHandle connector,
        IShapeHandle source,
        IShapeHandle target,
        int? sourceConnectionPoint,
        int? targetConnectionPoint);

    void SetLabel(IShapeHandle connector, string? label);

    void ApplyStyle(IShapeHandle connector, ConnectorStyle? style);

    int CountVerifiedEndpointConnections(
        IShapeHandle connector,
        IShapeHandle source,
        IShapeHandle target);

    void Delete(IShapeHandle connector);
}
