using System.Runtime.InteropServices;
using VisioTestBridge.Contracts;
using VisioTestBridge.Core.Abstractions;
using VisioTestBridge.Core.Interop;

namespace VisioTestBridge.Core.Files;

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

        var staging = Path.Combine(
            Path.GetDirectoryName(target)!,
            $".{Path.GetFileName(target)}.{_sessionId}.{Guid.NewGuid():N}.staging.vsdx");

        var stagingPhase = "staging_save";
        try
        {
            _saver.Save(staging);
            stagingPhase = "staging_validation";
            _validator.Validate(staging);
            EnsureNonEmpty(staging);
        }
        catch (Exception error)
        {
            throw BuildFailure(
                error,
                stagingPhase,
                restored: true,
                staging);
        }

        try
        {
            _saver.CloseForCommit();
        }
        catch (Exception error)
        {
            throw BuildFailure(
                error,
                "staging_close",
                restored: true,
                staging);
        }

        try
        {
            if (existed)
            {
                File.Replace(
                    staging,
                    target,
                    backup,
                    ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(staging, target);
            }
        }
        catch (Exception error)
        {
            var restored = RestoreAfterCommitFailure(
                target,
                backup,
                staging,
                existed);
            var reopenError = TryReopenRecovery(target, staging, existed, restored);
            throw BuildFailure(
                error,
                "commit",
                restored,
                staging,
                backup,
                committed: false,
                reopenError: reopenError);
        }

        try
        {
            _saver.Reopen(target);
        }
        catch (Exception error)
        {
            throw BuildFailure(
                error,
                "reopen",
                restored: false,
                staging,
                backup,
                committed: true);
        }

        try
        {
            if (File.Exists(backup)) File.Delete(backup);
        }
        catch (Exception error)
        {
            throw BuildFailure(
                error,
                "cleanup",
                restored: false,
                staging,
                backup,
                committed: true);
        }

        return new SaveResult(
            target,
            new FileInfo(target).Length,
            existed,
            true);
    }

    private static BridgeCommandException BuildFailure(
        Exception error,
        string phase,
        bool restored,
        string staging,
        string? backup = null,
        bool committed = false,
        Exception? reopenError = null)
    {
        var commandError = error as BridgeCommandException
            ?? (error is COMException comError
                ? ComExceptionTranslator.Translate(comError)
                : null);
        var validationFailure = phase == "staging_validation";
        var code = commandError?.Code
            ?? (validationFailure
                ? BridgeErrorCodes.ValidationFailed
                : BridgeErrorCodes.SaveFailed);
        return new BridgeCommandException(
            code,
            validationFailure
                ? $"VSDX 保存候选文件验证失败：{error.Message}"
                : $"VSDX 安全保存失败：{error.Message}",
            commandError?.Retryable ?? false,
            new
            {
                restored,
                committed,
                phase,
                reason = commandError?.Message ?? error.Message,
                recovery_path = File.Exists(staging) ? staging : null,
                backup_path = backup is not null && File.Exists(backup)
                    ? backup
                    : null,
                reopen_error = reopenError?.Message,
                original_details = commandError?.Details,
            });
    }

    private static void EnsureNonEmpty(string path)
    {
        if (!File.Exists(path) || new FileInfo(path).Length == 0)
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.ValidationFailed,
                "保存后的 VSDX 不存在或为空。");
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

    private Exception? TryReopenRecovery(
        string target,
        string staging,
        bool existed,
        bool restored)
    {
        if (!restored) return null;

        var recoveryDocument = existed ? target : staging;
        if (!File.Exists(recoveryDocument)) return null;
        try
        {
            _saver.Reopen(recoveryDocument);
            return null;
        }
        catch (Exception error)
        {
            return error;
        }
    }

    private static bool RestoreAfterCommitFailure(
        string target,
        string backup,
        string staging,
        bool existed)
    {
        try
        {
            if (existed)
            {
                if (!File.Exists(backup)) return File.Exists(target);
                if (File.Exists(target))
                {
                    File.Replace(
                        backup,
                        target,
                        destinationBackupFileName: null,
                        ignoreMetadataErrors: true);
                }
                else
                {
                    File.Move(backup, target);
                }
                return File.Exists(target) && !File.Exists(backup);
            }

            if (File.Exists(target) && !File.Exists(staging))
            {
                File.Move(target, staging);
            }
            return !File.Exists(target);
        }
        catch
        {
            return false;
        }
    }
}
