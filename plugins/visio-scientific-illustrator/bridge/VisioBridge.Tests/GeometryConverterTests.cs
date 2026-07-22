using VisioBridge.Contracts;
using VisioBridge.Core;
using VisioBridge.Core.Geometry;
using VisioBridge.Core.Session;

namespace VisioBridge.Tests;

public sealed class GeometryConverterTests
{
    [Theory]
    [InlineData(0, 0, 96, 96, 0.5, 9.9166666667)]
    [InlineData(120, 80, 240, 100, 2.5, 9.0625)]
    public void TopLeftPixels_convert_to_Visio_pin(
        double x,
        double y,
        double width,
        double height,
        double expectedPinX,
        double expectedPinY)
    {
        var converter = new GeometryConverter(new CanvasSpec(1600, 1000, 96));

        var box = converter.ToVisio(new PixelBox(x, y, width, height));

        Assert.Equal(expectedPinX, box.PinXInches, 8);
        Assert.Equal(expectedPinY, box.PinYInches, 8);
        var roundTrip = converter.ToPixels(box);
        Assert.Equal(x, roundTrip.X, 8);
        Assert.Equal(y, roundTrip.Y, 8);
        Assert.Equal(width, roundTrip.Width, 8);
        Assert.Equal(height, roundTrip.Height, 8);
    }

    [Fact]
    public void Non_finite_geometry_is_rejected()
    {
        var converter = new GeometryConverter(new CanvasSpec(1600, 1000, 96));

        var error = Assert.Throws<BridgeCommandException>(() =>
            converter.ToVisio(new PixelBox(double.NaN, 0, 100, 100)));

        Assert.Equal(BridgeErrorCodes.InvalidCoordinates, error.Code);
    }

    [Fact]
    public void Configured_margin_controls_outside_boxes()
    {
        var converter = new GeometryConverter(new CanvasSpec(1600, 1000, 96), marginPx: 20);

        converter.ToVisio(new PixelBox(-20, -20, 100, 100));
        Assert.Throws<BridgeCommandException>(() =>
            converter.ToVisio(new PixelBox(-21, 0, 100, 100)));
    }
}
