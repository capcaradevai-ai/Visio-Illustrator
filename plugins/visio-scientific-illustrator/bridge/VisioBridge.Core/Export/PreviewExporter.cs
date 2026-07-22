using System.Buffers.Binary;
using VisioBridge.Core.Abstractions;

namespace VisioBridge.Core.Export;

public sealed class PreviewExporter
{
    private static readonly byte[] PngSignature =
        [137, 80, 78, 71, 13, 10, 26, 10];

    private readonly IPreviewExportAdapter _adapter;
    private readonly string _sessionTempRoot;

    public PreviewExporter(
        IPreviewExportAdapter adapter,
        string sessionTempRoot)
    {
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
        if (!Path.IsPathFullyQualified(sessionTempRoot))
        {
            throw new ArgumentException("临时目录必须是绝对路径。", nameof(sessionTempRoot));
        }
        _sessionTempRoot = sessionTempRoot;
    }

    public PreviewResult Export(int widthPx)
    {
        if (widthPx is < 128 or > 10000)
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.InvalidArgument,
                "width_px 必须在 128–10000 范围内。");
        }

        var settings = _adapter.ReadSettings();
        var directory = Path.Combine(
            _sessionTempRoot,
            "previews",
            Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "preview.png");
        Directory.CreateDirectory(directory);
        try
        {
            _adapter.ApplyPreviewSettings(widthPx);
            _adapter.ExportPage(path);
            var dimensions = ValidatePng(path);
            return new PreviewResult(
                Path.GetFullPath(path),
                dimensions.Width,
                dimensions.Height,
                new FileInfo(path).Length);
        }
        catch (BridgeCommandException)
        {
            throw;
        }
        catch (Exception error)
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.ExportFailed,
                "Visio PNG 预览导出失败。",
                details: new { reason = error.Message });
        }
        finally
        {
            _adapter.RestoreSettings(settings);
        }
    }

    private static (int Width, int Height) ValidatePng(string path)
    {
        if (!File.Exists(path))
        {
            throw InvalidPreview("导出文件不存在。");
        }
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length < 33 || !bytes.AsSpan(0, 8).SequenceEqual(PngSignature))
        {
            throw InvalidPreview("PNG signature 或 IHDR 无效。");
        }
        if (!bytes.AsSpan(12, 4).SequenceEqual("IHDR"u8))
        {
            throw InvalidPreview("PNG 缺少 IHDR。");
        }
        var width = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4));
        var height = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4));
        if (width <= 0 || height <= 0 || !ContainsIend(bytes))
        {
            throw InvalidPreview("PNG 尺寸或 IEND 无效。");
        }
        return (width, height);
    }

    private static bool ContainsIend(byte[] bytes)
    {
        for (var index = 8; index <= bytes.Length - 8; index++)
        {
            if (bytes.AsSpan(index, 4).SequenceEqual("IEND"u8)) return true;
        }
        return false;
    }

    private static BridgeCommandException InvalidPreview(string reason) =>
        new(
            BridgeErrorCodes.ExportFailed,
            "Visio 返回了无效的 PNG 预览。",
            details: new { reason });
}
