using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;
using VisioBridge.Contracts;
using VisioBridge.Core.Export;
using VisioBridge.Core.Geometry;
using VisioBridge.Core.Inspection;
using VisioBridge.Core.Interop;
using VisioBridge.Core.Session;
using Visio = Microsoft.Office.Interop.Visio;

namespace VisioBridge.Core.Files;

public sealed class VisioFileModeService : IDisposable
{
    private static readonly byte[] PngSignature =
        [137, 80, 78, 71, 13, 10, 26, 10];

    private readonly StaCommandDispatcher _dispatcher = new();
    private bool _disposed;

    public FileValidationResult Validate(ValidateFileCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var input = ValidateInputPath(command.InputPath);
        return _dispatcher.InvokeAsync(() => WithDocument(input, (_, document) =>
        {
            new VisioDocumentValidator().Validate(input);
            var counts = Count(document);
            return new FileValidationResult(
                input,
                counts.Pages,
                counts.Shapes,
                counts.Connectors,
                true);
        })).GetAwaiter().GetResult();
    }

    public FileInspectionResult Inspect(InspectFileCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.MaxShapes is < 1 or > 5000)
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.InvalidArgument,
                "max_shapes 必须在 1–5000 范围内。");
        }
        var input = ValidateInputPath(command.InputPath);
        return _dispatcher.InvokeAsync(() => WithDocument(input, (application, document) =>
        {
            new VisioDocumentValidator().Validate(input);
            var results = new List<PageInspectionResult>();
            var pages = document.Pages;
            try
            {
                var remaining = command.MaxShapes;
                for (var index = 1; index <= pages.Count && remaining > 0; index++)
                {
                    var page = pages[index];
                    try
                    {
                        var canvas = ReadCanvas(page, 96);
                        var result = new PageInspector(
                            document,
                            page,
                            new GeometryConverter(canvas)).Inspect(remaining);
                        results.Add(result);
                        remaining -= result.Shapes.Count;
                    }
                    finally
                    {
                        Release(ref page);
                    }
                }
            }
            finally
            {
                Release(ref pages);
            }
            return new FileInspectionResult(
                input,
                application.Version,
                true,
                results,
                []);
        })).GetAwaiter().GetResult();
    }

    public FileExportResult Export(ExportFileCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var input = ValidateInputPath(command.InputPath);
        var format = command.Format.ToLowerInvariant();
        var output = FileExportPolicy.ValidateOutputPath(command.OutputPath, format);
        ValidateScope(format, command.PageScope);
        if (format == "png" && (command.Width is < 128 or > 10000))
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.InvalidArgument,
                "width 必须在 128–10000 范围内。");
        }
        var existed = File.Exists(output);
        if (existed && !command.Overwrite)
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.OutputExists,
                $"输出文件已存在: {output}");
        }

        return _dispatcher.InvokeAsync(() => WithDocument(input, (application, document) =>
        {
            new VisioDocumentValidator().Validate(input);
            var directory = Path.GetDirectoryName(output)!;
            var temp = Path.Combine(
                directory,
                $".{Path.GetFileName(output)}.{Guid.NewGuid():N}.tmp{Path.GetExtension(output)}");
            var backup = Path.Combine(
                directory,
                $".{Path.GetFileName(output)}.{Guid.NewGuid():N}.backup");
            try
            {
                var page = application.ActivePage;
                try
                {
                    ExportCore(application, document, page, command, format, temp);
                }
                finally
                {
                    Release(ref page);
                }
                ValidateExport(temp, format, format == "png" ? command.Width : null);
                CommitOutput(temp, output, backup, existed);
                return new FileExportResult(
                    input,
                    output,
                    format,
                    command.PageScope,
                    new FileInfo(output).Length,
                    existed);
            }
            catch (BridgeCommandException)
            {
                throw;
            }
            catch (COMException error)
            {
                throw ComExceptionTranslator.Translate(error);
            }
            catch (Exception error)
            {
                throw new BridgeCommandException(
                    BridgeErrorCodes.ExportFailed,
                    $"Visio {format.ToUpperInvariant()} 导出失败。",
                    details: new { reason = error.Message });
            }
            finally
            {
                if (File.Exists(temp)) File.Delete(temp);
                if (File.Exists(backup)) File.Delete(backup);
            }
        })).GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _dispatcher.Dispose();
    }

    private static T WithDocument<T>(
        string input,
        Func<Visio.InvisibleApp, Visio.Document, T> operation)
    {
        Visio.InvisibleApp? application = null;
        Visio.Document? document = null;
        try
        {
            application = new Visio.InvisibleApp();
            var documents = application.Documents;
            try
            {
                var flags = (short)(
                    Visio.VisOpenSaveArgs.visOpenRO
                    | Visio.VisOpenSaveArgs.visOpenHidden
                    | Visio.VisOpenSaveArgs.visOpenDontList
                    | Visio.VisOpenSaveArgs.visOpenMacrosDisabled
                    | Visio.VisOpenSaveArgs.visOpenDeclineAutoRefresh);
                document = documents.OpenEx(input, flags);
            }
            finally
            {
                Release(ref documents);
            }
            return operation(application, document);
        }
        finally
        {
            try
            {
                document?.Close();
                application?.Quit();
            }
            catch (COMException)
            {
            }
            Release(ref document);
            Release(ref application);
        }
    }

    private static void ExportCore(
        Visio.InvisibleApp application,
        Visio.Document document,
        Visio.Page page,
        ExportFileCommand command,
        string format,
        string temp)
    {
        if (format == "png")
        {
            var adapter = new VisioPreviewExportAdapter(application, page);
            var before = adapter.ReadSettings();
            try
            {
                adapter.ApplyPreviewSettings(command.Width);
                adapter.ExportPage(temp);
            }
            finally
            {
                adapter.RestoreSettings(before);
            }
            return;
        }
        if (format == "svg")
        {
            page.Export(temp);
            return;
        }
        var range = command.PageScope == "all"
            ? Visio.VisPrintOutRange.visPrintAll
            : Visio.VisPrintOutRange.visPrintCurrentPage;
        document.ExportAsFixedFormat(
            Visio.VisFixedFormatTypes.visFixedFormatPDF,
            temp,
            Visio.VisDocExIntent.visDocExIntentPrint,
            range,
            1,
            1,
            false,
            true,
            true,
            true,
            false,
            Type.Missing);
    }

    private static void CommitOutput(
        string temp,
        string output,
        string backup,
        bool existed)
    {
        if (existed)
        {
            File.Replace(temp, output, backup, ignoreMetadataErrors: true);
            File.Delete(backup);
        }
        else
        {
            File.Move(temp, output);
        }
    }

    private static void ValidateExport(string path, string format, int? expectedWidth)
    {
        if (!File.Exists(path) || new FileInfo(path).Length == 0)
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.ExportFailed,
                "导出文件不存在或为空。");
        }
        var bytes = File.ReadAllBytes(path);
        var valid = format switch
        {
            "png" => bytes.Length >= 24
                && bytes.AsSpan(0, 8).SequenceEqual(PngSignature)
                && (!expectedWidth.HasValue
                    || BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4)) == expectedWidth.Value),
            "pdf" => bytes.AsSpan(0, Math.Min(bytes.Length, 5)).SequenceEqual("%PDF-"u8),
            "svg" => Encoding.UTF8.GetString(bytes, 0, Math.Min(bytes.Length, 4096))
                .Contains("<svg", StringComparison.OrdinalIgnoreCase),
            _ => false,
        };
        if (!valid)
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.ExportFailed,
                $"导出的 {format.ToUpperInvariant()} 签名或尺寸无效。");
        }
    }

    private static (int Pages, int Shapes, int Connectors) Count(Visio.Document document)
    {
        var shapes = 0;
        var connectors = 0;
        var pages = document.Pages;
        try
        {
            for (var index = 1; index <= pages.Count; index++)
            {
                var page = pages[index];
                try
                {
                    CountShapes(page.Shapes, ref shapes, ref connectors);
                }
                finally
                {
                    Release(ref page);
                }
            }
            return (pages.Count, shapes, connectors);
        }
        finally
        {
            Release(ref pages);
        }
    }

    private static void CountShapes(
        Visio.Shapes collection,
        ref int shapes,
        ref int connectors)
    {
        try
        {
            for (var index = 1; index <= collection.Count; index++)
            {
                var shape = collection[index];
                try
                {
                    if (shape.OneD != 0) connectors++;
                    else shapes++;
                    var children = shape.Shapes;
                    if (children.Count > 0) CountShapes(children, ref shapes, ref connectors);
                    else Release(ref children);
                }
                finally
                {
                    Release(ref shape);
                }
            }
        }
        finally
        {
            Marshal.ReleaseComObject(collection);
        }
    }

    private static CanvasSpec ReadCanvas(Visio.Page page, int dpi)
    {
        var sheet = page.PageSheet;
        Visio.Cell? width = null;
        Visio.Cell? height = null;
        try
        {
            width = sheet.CellsU["PageWidth"];
            height = sheet.CellsU["PageHeight"];
            return new CanvasSpec(
                checked((int)Math.Round(width.ResultIU * dpi)),
                checked((int)Math.Round(height.ResultIU * dpi)),
                dpi);
        }
        finally
        {
            Release(ref height);
            Release(ref width);
            Release(ref sheet);
        }
    }

    private static string ValidateInputPath(string path)
    {
        if (!Path.IsPathFullyQualified(path)
            || !string.Equals(Path.GetExtension(path), ".vsdx", StringComparison.OrdinalIgnoreCase)
            || !File.Exists(path))
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.InvalidArgument,
                "input_path 必须是已存在的绝对 .vsdx 路径。");
        }
        return Path.GetFullPath(path);
    }

    private static void ValidateScope(string format, string scope)
    {
        if (scope is not ("current" or "all")
            || format is not "pdf" && scope != "current")
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.InvalidArgument,
                format == "pdf"
                    ? "PDF page_scope 仅允许 current 或 all。"
                    : $"{format.ToUpperInvariant()} page_scope 必须为 current。");
        }
    }

    private static void Release<T>(ref T? value)
        where T : class
    {
        if (value is not null && Marshal.IsComObject(value))
        {
            Marshal.ReleaseComObject(value);
        }
        value = null;
    }
}
