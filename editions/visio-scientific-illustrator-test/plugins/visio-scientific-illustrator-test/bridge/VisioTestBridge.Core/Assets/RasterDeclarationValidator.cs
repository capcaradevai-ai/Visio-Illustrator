using System.Security.Cryptography;
using VisioTestBridge.Contracts;

namespace VisioTestBridge.Core.Assets;

public static class RasterDeclarationValidator
{
    private static readonly HashSet<string> SourceTypes =
        new(["reference_crop", "user_file", "generated_external"], StringComparer.Ordinal);

    public static RasterValidationResult Validate(
        string filePath,
        PixelBox targetBounds,
        RasterAssetDeclaration declaration)
    {
        ArgumentNullException.ThrowIfNull(declaration);

        if (string.IsNullOrWhiteSpace(filePath)
            || !Path.IsPathFullyQualified(filePath)
            || Uri.TryCreate(filePath, UriKind.Absolute, out var uri) && !uri.IsFile
            || !File.Exists(filePath))
        {
            throw Invalid("图片必须来自已存在的本地绝对文件路径。");
        }

        ValidateHash(declaration.AssetSha256, "asset_sha256");
        var actualHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(filePath)))
            .ToLowerInvariant();
        if (!string.Equals(actualHash, declaration.AssetSha256, StringComparison.Ordinal))
            throw Invalid("asset_sha256 与图片文件不一致。");

        if (!SourceTypes.Contains(declaration.SourceType))
            throw Invalid($"不支持 source_type：{declaration.SourceType}");
        if (!declaration.SourceIsTightlyCropped)
            throw Invalid("source_is_tightly_cropped 必须为 true。");
        if (!declaration.AtomicRasterUnit)
            throw Invalid("atomic_raster_unit 必须为 true。");
        if (declaration.ContainsReconstructableContent)
            throw Invalid("contains_reconstructable_content 必须为 false。");
        if (!string.Equals(declaration.EditableScope, "object_only", StringComparison.Ordinal))
            throw Invalid("editable_scope 必须是 object_only。");

        ValidateText(declaration.AssetId, "asset_id", 120);
        ValidateText(declaration.RasterReason, "raster_reason", 2000);
        ValidateText(declaration.DecompositionNote, "decomposition_note", 2000);
        ValidateText(declaration.SemanticRole, "semantic_role", 120);

        switch (declaration.SourceType)
        {
            case "reference_crop":
                if (declaration.SourceReferenceSha256 is null || declaration.SourceBounds is null)
                    throw Invalid("reference_crop 必须包含源参考图哈希和裁剪边界。");
                ValidateHash(declaration.SourceReferenceSha256, "source_reference_sha256");
                ValidateBounds(declaration.SourceBounds, "source_bounds");
                if (declaration.ProvenanceNote is not null)
                    ValidateText(declaration.ProvenanceNote, "provenance_note", 2000);
                break;
            case "user_file":
                if (declaration.SourceReferenceSha256 is not null
                    || declaration.SourceBounds is not null
                    || declaration.ProvenanceNote is not null)
                {
                    throw Invalid("user_file 不得声明参考图裁剪或外部生成来源。");
                }
                break;
            case "generated_external":
                if (declaration.SourceReferenceSha256 is not null || declaration.SourceBounds is not null)
                    throw Invalid("generated_external 不得声明参考图裁剪来源。");
                ValidateText(declaration.ProvenanceNote, "provenance_note", 2000);
                break;
        }

        ValidateBounds(targetBounds, "bounds");
        if (targetBounds.Width * targetBounds.Height > 16_000_000)
            throw Invalid("图片目标面积超过 1600 万像素限制。");

        return new RasterValidationResult(
            declaration.AssetId,
            actualHash,
            declaration.SourceType,
            declaration.SemanticRole);
    }

    private static void ValidateBounds(PixelBox bounds, string name)
    {
        if (!double.IsFinite(bounds.X)
            || !double.IsFinite(bounds.Y)
            || !double.IsFinite(bounds.Width)
            || !double.IsFinite(bounds.Height)
            || bounds.X < 0
            || bounds.Y < 0
            || bounds.Width <= 0
            || bounds.Height <= 0)
        {
            throw Invalid($"{name} 必须是非负位置和正尺寸的有限像素边界。");
        }
    }

    private static void ValidateText(string? value, string name, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length > maxLength
            || value.Any(character => char.IsControl(character)
                && character is not '\r' and not '\n' and not '\t'))
        {
            throw Invalid($"{name} 必须是 1–{maxLength} 个无非法控制字符的文本。");
        }
    }

    private static void ValidateHash(string? value, string name)
    {
        if (value is null
            || value.Length != 64
            || value.Any(character =>
                character is not (>= '0' and <= '9')
                and not (>= 'a' and <= 'f')))
        {
            throw Invalid($"{name} 必须是 64 位小写 SHA-256。");
        }
    }

    private static BridgeCommandException Invalid(string message) =>
        new(BridgeErrorCodes.RasterDeclarationInvalid, message);
}
