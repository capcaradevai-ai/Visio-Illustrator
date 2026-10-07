using VisioBridge.Contracts;

namespace VisioBridge.Core.Abstractions;

public interface IConnectorAdapter
{
    IShapeHandle Create(
        IShapeHandle source,
        IShapeHandle target,
        string routing,
        IReadOnlyList<PixelPoint>? waypoints,
        IReadOnlyList<IShapeHandle> avoidShapes,
        ConnectorAnchor sourceAnchor,
        ConnectorAnchor targetAnchor,
        string connectorSemanticId);

    void Glue(
        IShapeHandle connector,
        IShapeHandle source,
        IShapeHandle target,
        ConnectorAnchor sourceAnchor,
        ConnectorAnchor targetAnchor,
        string connectorSemanticId);

    void SetLabel(IShapeHandle connector, string? label, double? labelPosition);

    void ApplyRoutingOptions(IShapeHandle connector, string? jumpStyle);

    void ApplyStyle(IShapeHandle connector, ConnectorStyle? style);

    int CountVerifiedEndpointConnections(
        IShapeHandle connector,
        IShapeHandle source,
        IShapeHandle target);

    void Delete(IShapeHandle connector);
}
