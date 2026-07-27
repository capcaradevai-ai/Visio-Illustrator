using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using VisioTestBridge.Contracts;
using VisioTestBridge.Core;
using VisioTestBridge.Core.Abstractions;
using VisioTestBridge.Core.Files;
using VisioTestBridge.Core.Session;
using Visio = Microsoft.Office.Interop.Visio;

namespace VisioTestBridge.IntegrationTests;

public sealed class SaveIntegrationTests
{
    [Trait("Category", "VisioIntegration")]
    [Fact]
    public void Save_creates_a_valid_reopenable_vsdx()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "visio-bridge-integration",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var target = Path.Combine(directory, "figure.vsdx");
        try
        {
            using (var fixture = VisioFixture.StartVisible())
            {
                fixture.AddShape(new AddShapeCommand(
                    "saved-shape",
                    "rectangle",
                    new PixelBox(100, 100, 300, 150),
                    "Saved"));
                var result = fixture.Save(target);

                Assert.True(result.Validated);
                Assert.True(result.Bytes > 1000);
                Assert.True(File.Exists(target));
                Assert.Equal(SessionState.Ready, fixture.GetStatus().State);
                Assert.Equal("figure.vsdx", fixture.GetStatus().DocumentName);
                var saveState = fixture.ReadDocumentSaveState();
                Assert.Equal(Path.GetFullPath(target), Path.GetFullPath(saveState.FullName));
                Assert.True(saveState.Saved);

                fixture.UpdateShape(new UpdateShapeCommand(
                    "saved-shape",
                    Label: "Edited after commit"));
                var reopenedShape = Assert.Single(fixture.Inspect().Shapes);
                Assert.Equal("Edited after commit", reopenedShape.Label);
                Assert.Empty(Directory.GetFiles(directory, "*.staging.vsdx"));
                Assert.Empty(Directory.GetFiles(directory, "*.backup"));
            }
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Trait("Category", "VisioIntegration")]
    [Fact]
    public async Task Validation_failure_after_real_Visio_save_restores_original_hash()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "visio-bridge-integration",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var target = Path.Combine(directory, "protected.vsdx");
        try
        {
            using var dispatcher = new StaCommandDispatcher();
            await dispatcher.InvokeAsync(() =>
            {
                Visio.InvisibleApp? application = null;
                Visio.Document? document = null;
                Visio.Page? page = null;
                Visio.Shape? shape = null;
                try
                {
                    application = new Visio.InvisibleApp();
                    document = AddDocument(application);
                    page = GetFirstPage(document);
                    document.SaveAsEx(target, 0);
                    page = Release(page);
                    document.Close();
                    document = Release(document);
                    var before = SHA256.HashData(File.ReadAllBytes(target));

                    document = OpenDocument(application, target);
                    page = GetFirstPage(document);
                    shape = page.DrawRectangle(1, 1, 3, 2);
                    var writer = new SafeFileWriter(
                        new VisioDocumentSaver(
                            () => document!,
                            () => throw new InvalidOperationException(
                                "验证失败时不应关闭暂存文档。"),
                            _ => throw new InvalidOperationException(
                                "验证失败时不应重新打开文档。")),
                        new InjectedFailureValidator(),
                        "real-visio-validation-failure");

                    var error = Assert.Throws<BridgeCommandException>(() =>
                        writer.Save(target, overwrite: true));
                    var details = JsonSerializer.SerializeToElement(error.Details);
                    var stagingPath = details
                        .GetProperty("recovery_path")
                        .GetString();
                    Assert.NotNull(stagingPath);
                    Assert.Equal(
                        Path.GetFullPath(stagingPath),
                        Path.GetFullPath(document.FullName));
                    Assert.True(document.Saved);
                    shape = Release(shape);
                    document.Saved = true;
                    document.Close();
                    page = Release(page);
                    document = Release(document);
                    var after = SHA256.HashData(File.ReadAllBytes(target));

                    Assert.Equal(BridgeErrorCodes.ValidationFailed, error.Code);
                    Assert.True(details.GetProperty("restored").GetBoolean());
                    Assert.Equal(
                        "staging_validation",
                        details.GetProperty("phase").GetString());
                    Assert.True(File.Exists(stagingPath));
                    Assert.Equal(before, after);
                }
                finally
                {
                    if (shape is not null) Marshal.ReleaseComObject(shape);
                    if (document is not null)
                    {
                        document.Saved = true;
                        document.Close();
                    }
                    application?.Quit();
                    if (page is not null) Marshal.ReleaseComObject(page);
                    if (document is not null) Marshal.ReleaseComObject(document);
                    if (application is not null) Marshal.ReleaseComObject(application);
                }
            });
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class InjectedFailureValidator : IDocumentValidator
    {
        public void Validate(string path) => throw new BridgeCommandException(
            BridgeErrorCodes.ValidationFailed,
            "Injected validation failure after real Visio save.");
    }

    private static T? Release<T>(T? value)
        where T : class
    {
        if (value is not null) Marshal.ReleaseComObject(value);
        return null;
    }

    private static Visio.Document AddDocument(Visio.InvisibleApp application)
    {
        var documents = application.Documents;
        try
        {
            return documents.Add("");
        }
        finally
        {
            Marshal.ReleaseComObject(documents);
        }
    }

    private static Visio.Document OpenDocument(
        Visio.InvisibleApp application,
        string path)
    {
        var documents = application.Documents;
        try
        {
            return documents.Open(path);
        }
        finally
        {
            Marshal.ReleaseComObject(documents);
        }
    }

    private static Visio.Page GetFirstPage(Visio.Document document)
    {
        var pages = document.Pages;
        try
        {
            return pages[1];
        }
        finally
        {
            Marshal.ReleaseComObject(pages);
        }
    }
}
