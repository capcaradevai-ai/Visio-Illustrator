using Visio = Microsoft.Office.Interop.Visio;

namespace VisioBridge.Core.Shapes;

internal static class VisioShapeClassifier
{
    internal static bool IsConnector(Visio.Shape shape)
        => IsConnector(
            shape.OneD,
            shape.Type == (short)Visio.VisShapeTypes.visTypeGroup);

    internal static bool IsConnector(short oneD, bool isGroup)
        => oneD != 0 && !isGroup;

    internal static bool IsOneDimensionalGroup(Visio.Shape shape)
        => IsOneDimensionalGroup(
            shape.OneD,
            shape.Type == (short)Visio.VisShapeTypes.visTypeGroup);

    internal static bool IsOneDimensionalGroup(short oneD, bool isGroup)
        => oneD != 0 && isGroup;
}
