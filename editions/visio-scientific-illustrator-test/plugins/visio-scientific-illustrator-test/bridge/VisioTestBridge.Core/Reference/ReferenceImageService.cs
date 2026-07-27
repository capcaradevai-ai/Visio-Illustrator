using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using VisioTestBridge.Contracts;

namespace VisioTestBridge.Core.Reference;

public sealed class ReferenceImageService
{
    private static readonly HashSet<string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg" };

    public InspectReferenceImageResult Inspect(InspectReferenceImageCommand command)
    {
        var sourcePath = ValidateSourcePath(command.SourcePath);
        using var bitmap = LoadNormalizedBitmap(sourcePath);
        var analysis = Analyze(bitmap);
        return new InspectReferenceImageResult(
            bitmap.Width,
            bitmap.Height,
            analysis.Palette,
            analysis.ForegroundBounds,
            "top_left_unrotated");
    }

    public CropReferenceImageResult Crop(CropReferenceImageCommand command)
    {
        var sourcePath = ValidateSourcePath(command.SourcePath);
        var outputPath = ValidateOutputPath(command.OutputPath);
        using var source = LoadNormalizedBitmap(sourcePath);
        ValidateBounds(command.Bounds, source.Width, source.Height);
        using var crop = new Bitmap(
            command.Bounds.Width,
            command.Bounds.Height,
            PixelFormat.Format32bppArgb);
        crop.SetResolution(96, 96);
        using (var graphics = Graphics.FromImage(crop))
        {
            graphics.CompositingMode = CompositingMode.SourceCopy;
            graphics.DrawImage(
                source,
                new Rectangle(0, 0, crop.Width, crop.Height),
                new Rectangle(
                    command.Bounds.X,
                    command.Bounds.Y,
                    command.Bounds.Width,
                    command.Bounds.Height),
                GraphicsUnit.Pixel);
        }

        var directory = Path.GetDirectoryName(outputPath)
            ?? throw InvalidManifest("output_path must include a directory.");
        Directory.CreateDirectory(directory);
        var candidate = Path.Combine(
            directory,
            $".{Path.GetFileName(outputPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            crop.Save(candidate, ImageFormat.Png);
            File.Move(candidate, outputPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(candidate)) File.Delete(candidate);
        }

        var bytes = File.ReadAllBytes(outputPath);
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var blankCount = CountBlankPixels(crop);
        return new CropReferenceImageResult(
            outputPath,
            crop.Width,
            crop.Height,
            hash,
            blankCount / (double)(crop.Width * crop.Height));
    }

    private static Bitmap LoadNormalizedBitmap(string sourcePath)
    {
        try
        {
            using var source = new Bitmap(sourcePath);
            var normalized = new Bitmap(
                source.Width,
                source.Height,
                PixelFormat.Format32bppArgb);
            normalized.SetResolution(96, 96);
            using var graphics = Graphics.FromImage(normalized);
            graphics.CompositingMode = CompositingMode.SourceCopy;
            graphics.DrawImageUnscaled(source, 0, 0);
            return normalized;
        }
        catch (BridgeCommandException)
        {
            throw;
        }
        catch (Exception error) when (
            error is ArgumentException
            or ExternalException
            or OutOfMemoryException)
        {
            throw InvalidManifest(
                $"Reference image could not be decoded: {error.Message}");
        }
    }

    private static ReferenceAnalysis Analyze(Bitmap bitmap)
    {
        var histogram = new Dictionary<int, ColorBucket>();
        var minX = bitmap.Width;
        var minY = bitmap.Height;
        var maxX = -1;
        var maxY = -1;

        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                var color = bitmap.GetPixel(x, y);
                if (IsBlank(color)) continue;
                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
                var key = ((color.R >> 4) << 8) | ((color.G >> 4) << 4) | (color.B >> 4);
                histogram.TryGetValue(key, out var bucket);
                histogram[key] = bucket.Add(color);
            }
        }

        var palette = histogram
            .OrderByDescending(pair => pair.Value.Count)
            .ThenBy(pair => pair.Key)
            .Take(12)
            .Select(pair => pair.Value.ToHex())
            .ToArray();
        var foreground = maxX < minX || maxY < minY
            ? new PixelBounds(0, 0, bitmap.Width, bitmap.Height)
            : new PixelBounds(minX, minY, maxX - minX + 1, maxY - minY + 1);
        return new ReferenceAnalysis(palette, foreground);
    }

    private static int CountBlankPixels(Bitmap bitmap)
    {
        var count = 0;
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                if (IsBlank(bitmap.GetPixel(x, y))) count++;
            }
        }
        return count;
    }

    private static bool IsBlank(Color color)
    {
        if (color.A <= 16) return true;
        var luminance = 0.2126 * color.R + 0.7152 * color.G + 0.0722 * color.B;
        return luminance >= 250;
    }

    private static string ValidateSourcePath(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !Path.IsPathFullyQualified(sourcePath))
        {
            throw InvalidManifest("source_path must be absolute.");
        }
        var normalized = Path.GetFullPath(sourcePath);
        if (!SupportedExtensions.Contains(Path.GetExtension(normalized)))
        {
            throw InvalidManifest("Only PNG and JPEG reference images are supported.");
        }
        if (!File.Exists(normalized))
        {
            throw InvalidManifest($"Reference image does not exist: {normalized}");
        }
        return normalized;
    }

    private static string ValidateOutputPath(string outputPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath)
            || !Path.IsPathFullyQualified(outputPath)
            || !string.Equals(Path.GetExtension(outputPath), ".png", StringComparison.OrdinalIgnoreCase))
        {
            throw InvalidManifest("output_path must be an absolute PNG path.");
        }
        return Path.GetFullPath(outputPath);
    }

    private static void ValidateBounds(PixelBounds bounds, int width, int height)
    {
        if (bounds.X < 0 || bounds.Y < 0 || bounds.Width <= 0 || bounds.Height <= 0
            || (long)bounds.X + bounds.Width > width
            || (long)bounds.Y + bounds.Height > height)
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.ReferenceCropInvalid,
                "Crop bounds exceed the reference image.");
        }
    }

    private static BridgeCommandException InvalidManifest(string message) =>
        new(BridgeErrorCodes.ReferenceManifestInvalid, message);

    private sealed record ReferenceAnalysis(
        IReadOnlyList<string> Palette,
        PixelBounds ForegroundBounds);

    private readonly record struct ColorBucket(long Red, long Green, long Blue, int Count)
    {
        public ColorBucket Add(Color color) =>
            new(Red + color.R, Green + color.G, Blue + color.B, Count + 1);

        public string ToHex() =>
            $"#{Red / Count:x2}{Green / Count:x2}{Blue / Count:x2}";
    }
}
