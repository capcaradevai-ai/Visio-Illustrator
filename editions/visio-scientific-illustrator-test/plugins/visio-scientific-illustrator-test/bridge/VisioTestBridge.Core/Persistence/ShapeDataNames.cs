namespace VisioTestBridge.Core.Persistence;

public static class ShapeDataNames
{
    private static readonly string[] RasterSuffixes =
    [
        "VsiRasterAssetId",
        "VsiRasterSourceType",
        "VsiRasterReason",
        "VsiRasterTightlyCropped",
        "VsiRasterAtomicUnit",
        "VsiRasterContainsReconstructable",
        "VsiRasterDecompositionNote",
        "VsiRasterEditableScope",
        "VsiRasterSourceReferenceSha256",
        "VsiRasterSourceBoundsJson",
        "VsiRasterAssetSha256",
        "VsiRasterSemanticRole",
    ];

    public static IReadOnlyList<string> RasterUserCells { get; } =
        RasterSuffixes.Select(value => $"User.{value}").ToArray();

    public static IReadOnlyList<string> RasterPropertyCells { get; } =
        RasterSuffixes.Select(value => $"Prop.{value}").ToArray();
}
