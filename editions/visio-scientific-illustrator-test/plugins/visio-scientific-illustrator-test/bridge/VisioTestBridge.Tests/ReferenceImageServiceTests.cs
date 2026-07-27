using System.Drawing;
using System.Drawing.Imaging;
using VisioTestBridge.Contracts;
using VisioTestBridge.Core;
using VisioTestBridge.Core.Reference;

namespace VisioTestBridge.Tests;

public sealed class ReferenceImageServiceTests
{
    [Fact]
    public void Inspect_reports_dimensions_palette_foreground_and_fixed_orientation()
    {
        using var temp = Fakes.TestDirectory.Create();
        var source = Path.Combine(temp.Path, "source.png");
        WriteImage(source, 4, 3, bitmap =>
        {
            bitmap.SetPixel(1, 1, Color.Red);
            bitmap.SetPixel(2, 1, Color.Blue);
        });

        var result = new ReferenceImageService().Inspect(
            new InspectReferenceImageCommand(source));

        Assert.Equal(4, result.WidthPx);
        Assert.Equal(3, result.HeightPx);
        Assert.InRange(result.Palette.Count, 1, 12);
        Assert.Contains("#ff0000", result.Palette);
        Assert.Equal(new PixelBounds(1, 1, 2, 1), result.ForegroundBounds);
        Assert.Equal("top_left_unrotated", result.PixelOrientation);
    }

    [Fact]
    public void Crop_writes_metadata_free_rgba_png_with_deterministic_hash()
    {
        using var temp = Fakes.TestDirectory.Create();
        var source = Path.Combine(temp.Path, "source.png");
        var firstPath = Path.Combine(temp.Path, "first.png");
        var secondPath = Path.Combine(temp.Path, "second.png");
        WriteImage(source, 4, 4, bitmap =>
        {
            bitmap.SetPixel(1, 1, Color.Red);
            bitmap.SetPixel(2, 1, Color.Blue);
        });
        var commandBounds = new PixelBounds(1, 1, 2, 2);
        var service = new ReferenceImageService();

        var first = service.Crop(new CropReferenceImageCommand(source, commandBounds, firstPath));
        var second = service.Crop(new CropReferenceImageCommand(source, commandBounds, secondPath));

        Assert.Equal(2, first.WidthPx);
        Assert.Equal(2, first.HeightPx);
        Assert.Equal(first.Sha256, second.Sha256);
        Assert.Equal(0.5, first.BlankRatio, precision: 6);
        using var savedImage = Image.FromFile(firstPath);
        Assert.Equal(ImageFormat.Png.Guid, savedImage.RawFormat.Guid);
    }

    [Fact]
    public void Crop_rejects_bounds_outside_the_source()
    {
        using var temp = Fakes.TestDirectory.Create();
        var source = Path.Combine(temp.Path, "source.png");
        WriteImage(source, 4, 4, _ => { });

        var error = Assert.Throws<BridgeCommandException>(() =>
            new ReferenceImageService().Crop(new CropReferenceImageCommand(
                source,
                new PixelBounds(3, 0, 2, 1),
                Path.Combine(temp.Path, "crop.png"))));

        Assert.Equal(BridgeErrorCodes.ReferenceCropInvalid, error.Code);
    }

    [Fact]
    public void Inspect_rejects_unsupported_source_format()
    {
        using var temp = Fakes.TestDirectory.Create();
        var source = temp.WriteFile("source.gif", "not-an-image");

        var error = Assert.Throws<BridgeCommandException>(() =>
            new ReferenceImageService().Inspect(new InspectReferenceImageCommand(source)));

        Assert.Equal(BridgeErrorCodes.ReferenceManifestInvalid, error.Code);
    }

    private static void WriteImage(
        string path,
        int width,
        int height,
        Action<Bitmap> paint)
    {
        using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        bitmap.SetResolution(96, 96);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.Transparent);
        }
        paint(bitmap);
        bitmap.Save(path, ImageFormat.Png);
    }
}
