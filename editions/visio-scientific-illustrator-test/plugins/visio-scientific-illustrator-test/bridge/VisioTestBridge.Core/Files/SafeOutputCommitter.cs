using System.Security.Cryptography;

namespace VisioTestBridge.Core.Files;

internal interface IOutputReplacer
{
    void Replace(string candidate, string output);
}

internal sealed class SystemOutputReplacer : IOutputReplacer
{
    public void Replace(string candidate, string output) => File.Replace(
        candidate,
        output,
        destinationBackupFileName: null,
        ignoreMetadataErrors: true);
}

internal sealed class SafeOutputCommitter
{
    private readonly IOutputReplacer _replacer;

    public SafeOutputCommitter(IOutputReplacer? replacer = null)
    {
        _replacer = replacer ?? new SystemOutputReplacer();
    }

    public void Commit(
        string candidate,
        string output,
        string backup,
        bool existed)
    {
        if (existed)
        {
            File.Copy(output, backup, overwrite: false);
        }

        try
        {
            if (existed) _replacer.Replace(candidate, output);
            else File.Move(candidate, output);
        }
        catch (Exception error)
        {
            var restored = Restore(candidate, output, backup, existed);
            throw new BridgeCommandException(
                BridgeErrorCodes.ExportFailed,
                $"导出文件原子提交失败：{error.Message}",
                details: new
                {
                    restored,
                    committed = false,
                    phase = "commit",
                    reason = error.Message,
                    recovery_path = File.Exists(candidate) ? candidate : null,
                    backup_path = File.Exists(backup) ? backup : null,
                });
        }

        try
        {
            if (File.Exists(backup)) File.Delete(backup);
        }
        catch (Exception error)
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.ExportFailed,
                $"导出已提交，但旧文件备份清理失败：{error.Message}",
                details: new
                {
                    restored = false,
                    committed = true,
                    phase = "cleanup",
                    reason = error.Message,
                    backup_path = File.Exists(backup) ? backup : null,
                });
        }
    }

    private static bool Restore(
        string candidate,
        string output,
        string backup,
        bool existed)
    {
        try
        {
            if (existed)
            {
                if (!File.Exists(backup)) return false;
                if (!File.Exists(candidate) && File.Exists(output))
                {
                    File.Move(output, candidate);
                }
                File.Copy(backup, output, overwrite: true);
                return FilesEqual(output, backup);
            }

            if (!File.Exists(candidate) && File.Exists(output))
            {
                File.Move(output, candidate);
            }
            return !File.Exists(output);
        }
        catch
        {
            return false;
        }
    }

    private static bool FilesEqual(string first, string second)
    {
        if (!File.Exists(first) || !File.Exists(second)) return false;
        using var firstStream = File.OpenRead(first);
        using var secondStream = File.OpenRead(second);
        return SHA256.HashData(firstStream).SequenceEqual(
            SHA256.HashData(secondStream));
    }
}
