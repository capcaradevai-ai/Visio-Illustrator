using VisioTestBridge.Contracts;
using VisioTestBridge.Core.Persistence;

namespace VisioTestBridge.Tests;

public sealed class ShapeMetadataServiceTests
{
    [Fact]
    public void Raster_declaration_is_written_to_user_cells_and_shape_data()
    {
        var target = new MemoryRasterMetadataStore();
        var declaration = new RasterAssetDeclaration(
            AssetId: "router-icon-1",
            SourceType: "reference_crop",
            RasterReason: "Atomic router illustration.",
            SourceIsTightlyCropped: true,
            AtomicRasterUnit: true,
            ContainsReconstructableContent: false,
            DecompositionNote: "Labels and connectors are separate Visio objects.",
            EditableScope: "object_only",
            SourceReferenceSha256: new string('a', 64),
            SourceBounds: new PixelBox(12, 20, 80, 60),
            AssetSha256: new string('b', 64),
            SemanticRole: "wireless_router");

        ShapeMetadataService.WriteRaster(target, declaration);

        Assert.Equal(ShapeDataNames.RasterUserCells.Order(), target.User.Keys.Order());
        Assert.Equal(ShapeDataNames.RasterPropertyCells.Order(), target.Properties.Keys.Order());
        Assert.Equal("router-icon-1", target.User["User.VsiRasterAssetId"]);
        Assert.Equal("reference_crop", target.Properties["Prop.VsiRasterSourceType"]);
        Assert.Equal("true", target.User["User.VsiRasterAtomicUnit"]);
        Assert.Equal("false", target.User["User.VsiRasterContainsReconstructable"]);
        Assert.Equal(
            "{\"x\":12,\"y\":20,\"width\":80,\"height\":60}",
            target.User["User.VsiRasterSourceBoundsJson"]);
    }

    private sealed class MemoryRasterMetadataStore : IRasterMetadataStore
    {
        public Dictionary<string, string> User { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, string> Properties { get; } = new(StringComparer.Ordinal);

        public void WriteUserCell(string cellName, string value) => User[cellName] = value;

        public void WriteShapeData(string cellName, string label, string value)
        {
            Assert.Equal(cellName.Replace("Prop.", string.Empty), label);
            Properties[cellName] = value;
        }
    }
}
