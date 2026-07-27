using System.Runtime.InteropServices;
using VisioTestBridge.Core.Abstractions;
using VisioTestBridge.Core.Interop;
using Visio = Microsoft.Office.Interop.Visio;

namespace VisioTestBridge.Core.Files;

internal sealed class VisioDocumentValidator : IDocumentValidator
{
    public void Validate(string absoluteVsDxPath)
    {
        var validationDirectory = Path.Combine(
            Path.GetTempPath(),
            "visio-scientific-illustrator",
            "validation",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(validationDirectory);
        var validationCopy = Path.Combine(validationDirectory, "document.vsdx");
        Visio.InvisibleApp? application = null;
        Visio.Document? document = null;
        try
        {
            File.Copy(absoluteVsDxPath, validationCopy, overwrite: false);
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
                document = documents.OpenEx(validationCopy, flags);
            }
            finally
            {
                Release(ref documents);
            }

            ValidateDocument(document);
            document.Close();
            Release(ref document);
            application.Quit();
            Release(ref application);
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
                BridgeErrorCodes.ValidationFailed,
                $"无法用独立 Visio 实例重新打开并验证 VSDX：{error.Message}",
                details: new { reason = error.Message });
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
            if (File.Exists(validationCopy)) File.Delete(validationCopy);
            if (Directory.Exists(validationDirectory)) Directory.Delete(validationDirectory);
        }
    }

    private static void ValidateDocument(Visio.Document document)
    {
        var semanticIds = new HashSet<string>(StringComparer.Ordinal);
        var pages = document.Pages;
        try
        {
            if (pages.Count <= 0) throw Invalid("文档没有页面。");
            for (var pageIndex = 1; pageIndex <= pages.Count; pageIndex++)
            {
                var page = pages[pageIndex];
                try
                {
                    _ = page.ID;
                    if (string.IsNullOrWhiteSpace(page.Name)) throw Invalid("页面名称为空。");
                    ValidatePageSize(page);
                    ValidateShapes(page.Shapes, semanticIds);
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
    }

    private static void ValidatePageSize(Visio.Page page)
    {
        var sheet = page.PageSheet;
        Visio.Cell? width = null;
        Visio.Cell? height = null;
        try
        {
            width = sheet.CellsU["PageWidth"];
            height = sheet.CellsU["PageHeight"];
            if (width.ResultIU <= 0 || height.ResultIU <= 0)
            {
                throw Invalid("页面尺寸不是正数。");
            }
        }
        finally
        {
            Release(ref height);
            Release(ref width);
            Release(ref sheet);
        }
    }

    private static void ValidateShapes(
        Visio.Shapes shapes,
        ISet<string> semanticIds)
    {
        try
        {
            for (var index = 1; index <= shapes.Count; index++)
            {
                var shape = shapes[index];
                try
                {
                    var semanticId = ReadSemanticId(shape);
                    if (semanticId is not null && !semanticIds.Add(semanticId))
                    {
                        throw Invalid($"User.CodexId 重复: {semanticId}");
                    }
                    if (shape.OneD != 0 && semanticId is not null)
                    {
                        ValidateConnector(shape, semanticId);
                    }
                    var children = shape.Shapes;
                    if (children.Count > 0) ValidateShapes(children, semanticIds);
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
            Marshal.ReleaseComObject(shapes);
        }
    }

    private static void ValidateConnector(Visio.Shape shape, string semanticId)
    {
        var targetIds = new HashSet<int>();
        var connects = shape.Connects;
        try
        {
            for (var index = 1; index <= connects.Count; index++)
            {
                var connect = connects[index];
                Visio.Shape? target = null;
                try
                {
                    target = connect.ToSheet;
                    targetIds.Add(target.ID);
                }
                finally
                {
                    Release(ref target);
                    Release(ref connect);
                }
            }
        }
        finally
        {
            Release(ref connects);
        }
        if (targetIds.Count != 2)
        {
            throw Invalid($"Connector {semanticId} 没有两个有效粘附端点。");
        }
    }

    private static string? ReadSemanticId(Visio.Shape shape)
    {
        if (shape.CellExistsU[
                "User.CodexId",
                (short)Visio.VisExistsFlags.visExistsAnywhere] == 0)
        {
            return null;
        }
        var cell = shape.CellsU["User.CodexId"];
        try
        {
            return cell.ResultStrU[(object)Visio.VisUnitCodes.visUnitsString];
        }
        finally
        {
            Release(ref cell);
        }
    }

    private static BridgeCommandException Invalid(string reason) =>
        new(
            BridgeErrorCodes.ValidationFailed,
            "VSDX 结构验证失败。",
            details: new { reason });

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
