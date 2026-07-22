using System.Runtime.InteropServices;
using VisioBridge.Contracts;
using VisioBridge.Core.Abstractions;
using VisioBridge.Core.Interop;

namespace VisioBridge.Core.Files;

public sealed class SafeFileWriter
{
    private readonly IDocumentSaver _saver;
    private readonly IDocumentValidator _validator;
    private readonly string _sessionId;

    public SafeFileWriter(
        IDocumentSaver saver,
        IDocumentValidator validator,
        string sessionId)
    {
        _saver = saver ?? throw new ArgumentNullException(nameof(saver));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new ArgumentException("sessionId 不能为空。", nameof(sessionId));
        }
        _sessionId = sessionId;
    }

    public SaveResult Save(string absoluteVsDxPath, bool overwrite)
    {
        var target = ValidateTarget(absoluteVsDxPath);
        var existed = File.Exists(target);
        if (existed && !overwrite)
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.OutputExists,
                $"输出文件已存在: {target}");
        }

        var backup = Path.Combine(
            Path.GetDirectoryName(target)!,
            $".{Path.GetFileName(target)}.{_sessionId}.backup");
        if (File.Exists(backup))
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.OutputExists,
                $"安全保存备份已存在: {backup}");
        }

        var phase = "save";
        try
        {
            if (existed) File.Copy(target, backup, overwrite: false);
            _saver.Save(target);
            phase = "validation";
            _validator.Validate(target);
            if (!File.Exists(target) || new FileInfo(target).Length == 0)
            {
                throw new BridgeCommandException(
                    BridgeErrorCodes.ValidationFailed,
                    "保存后的 VSDX 不存在或为空。");
            }
            if (existed) File.Delete(backup);
            return new SaveResult(
                target,
                new FileInfo(target).Length,
                existed,
                true);
        }
        catch (Exception error)
        {
            var restored = Restore(target, backup, existed);
            var commandError = error as BridgeCommandException
                ?? (error is COMException comError
                    ? ComExceptionTranslator.Translate(comError)
                    : null);
            var code = commandError is not null
                ? commandError.Code
                : phase == "validation"
                    ? BridgeErrorCodes.ValidationFailed
                    : BridgeErrorCodes.SaveFailed;
            var retryable = commandError?.Retryable ?? false;
            throw new BridgeCommandException(
                code,
                phase == "validation"
                    ? $"VSDX 保存后验证失败：{error.Message}"
                    : $"VSDX 安全保存失败：{error.Message}",
                retryable,
                new
                {
                    restored,
                    phase,
                    reason = commandError?.Message ?? error.Message,
                    original_details = commandError?.Details,
                });
        }
    }

    private static string ValidateTarget(string path)
    {
        if (string.IsNullOrWhiteSpace(path)
            || !Path.IsPathFullyQualified(path)
            || !string.Equals(
                Path.GetExtension(path),
                ".vsdx",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.InvalidArgument,
                "output_path 必须是绝对 .vsdx 文件路径。");
        }
        var target = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(target);
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.InvalidArgument,
                "output_path 的父目录必须已存在。");
        }
        if (Directory.Exists(target))
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.InvalidArgument,
                "output_path 不能是目录。");
        }
        return target;
    }

    private static bool Restore(string target, string backup, bool existed)
    {
        try
        {
            if (existed)
            {
                if (!File.Exists(backup)) return false;
                File.Copy(backup, target, overwrite: true);
                File.Delete(backup);
                return File.Exists(target);
            }
            if (File.Exists(target)) File.Delete(target);
            return !File.Exists(target);
        }
        catch
        {
            return false;
        }
    }
}
