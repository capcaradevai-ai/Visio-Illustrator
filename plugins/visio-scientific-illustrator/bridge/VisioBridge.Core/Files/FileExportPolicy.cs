namespace VisioBridge.Core.Files;

public static class FileExportPolicy
{
    public static string SelectApi(string format) => format switch
    {
        "png" or "svg" => "Page.Export",
        "pdf" => "Document.ExportAsFixedFormat",
        _ => throw new BridgeCommandException(
            BridgeErrorCodes.UnsupportedOperation,
            $"不支持导出格式: {format}"),
    };

    public static string ValidateOutputPath(string path, string format)
    {
        SelectApi(format);
        if (!Path.IsPathFullyQualified(path)
            || !string.Equals(
                Path.GetExtension(path),
                $".{format}",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.InvalidArgument,
                $"output_path 必须是绝对 .{format} 文件路径。");
        }
        var result = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(result);
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.InvalidArgument,
                "output_path 的父目录必须已存在。");
        }
        return result;
    }
}
