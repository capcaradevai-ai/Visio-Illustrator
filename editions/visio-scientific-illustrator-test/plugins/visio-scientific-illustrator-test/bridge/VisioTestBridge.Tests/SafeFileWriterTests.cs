using System.Runtime.InteropServices;
using System.Text.Json;
using VisioTestBridge.Core.Abstractions;
using VisioTestBridge.Core;
using VisioTestBridge.Core.Files;
using VisioTestBridge.Tests.Fakes;

namespace VisioTestBridge.Tests;

public sealed class SafeFileWriterTests
{
    [Fact]
    public void Successful_overwrite_commits_the_exact_validated_candidate()
    {
        using var temp = TestDirectory.Create();
        var target = temp.WriteFile("figure.vsdx", "original");
        var saver = new RecordingDocumentSaver("validated-candidate");
        var writer = new SafeFileWriter(
            saver,
            new PassThroughDocumentValidator(),
            "test-session");

        var result = writer.Save(target, overwrite: true);

        Assert.Equal("validated-candidate", File.ReadAllText(target));
        Assert.True(result.Validated);
        Assert.True(result.Overwritten);
        Assert.True(saver.ClosedForCommit);
        Assert.Equal(target, saver.ReopenedPath);
        Assert.NotNull(saver.SavedPath);
        Assert.Equal(temp.Path, Path.GetDirectoryName(saver.SavedPath));
        Assert.False(File.Exists(saver.SavedPath));
        Assert.Empty(Directory.GetFiles(temp.Path, "*.backup"));
    }

    [Fact]
    public void Failed_overwrite_restores_original_file()
    {
        using var temp = TestDirectory.Create();
        var target = temp.WriteFile("figure.vsdx", "original");
        var writer = new SafeFileWriter(
            new ThrowingDocumentSaver(),
            new PassThroughDocumentValidator(),
            "test-session");

        var error = Assert.Throws<BridgeCommandException>(
            () => writer.Save(target, overwrite: true));

        Assert.Equal(BridgeErrorCodes.SaveFailed, error.Code);
        Assert.Equal("original", File.ReadAllText(target));
    }

    [Fact]
    public void Existing_output_is_rejected_without_overwrite()
    {
        using var temp = TestDirectory.Create();
        var target = temp.WriteFile("figure.vsdx", "original");
        var writer = new SafeFileWriter(
            new ThrowingDocumentSaver(),
            new PassThroughDocumentValidator(),
            "test-session");

        var error = Assert.Throws<BridgeCommandException>(
            () => writer.Save(target, overwrite: false));

        Assert.Equal(BridgeErrorCodes.OutputExists, error.Code);
        Assert.Equal("original", File.ReadAllText(target));
    }

    [Fact]
    public void Relative_or_non_vsdx_output_is_rejected()
    {
        using var temp = TestDirectory.Create();
        var writer = new SafeFileWriter(
            new ThrowingDocumentSaver(),
            new PassThroughDocumentValidator(),
            "test-session");

        var relative = Assert.Throws<BridgeCommandException>(
            () => writer.Save("figure.vsdx", overwrite: false));
        var wrongExtension = Assert.Throws<BridgeCommandException>(
            () => writer.Save(Path.GetFullPath("figure.vsdm"), overwrite: false));

        Assert.Equal(BridgeErrorCodes.InvalidArgument, relative.Code);
        Assert.Equal(BridgeErrorCodes.InvalidArgument, wrongExtension.Code);
    }

    [Fact]
    public void Busy_COM_save_restores_original_and_remains_retryable()
    {
        using var temp = TestDirectory.Create();
        var target = temp.WriteFile("figure.vsdx", "original");
        var writer = new SafeFileWriter(
            new BusyDocumentSaver(),
            new PassThroughDocumentValidator(),
            "test-session");

        var error = Assert.Throws<BridgeCommandException>(
            () => writer.Save(target, overwrite: true));

        Assert.Equal(BridgeErrorCodes.VisioBusy, error.Code);
        Assert.True(error.Retryable);
        Assert.Equal("original", File.ReadAllText(target));
    }

    [Fact]
    public void Failed_atomic_commit_preserves_original_and_reopens_it()
    {
        using var temp = TestDirectory.Create();
        var target = temp.WriteFile("figure.vsdx", "original");
        var saver = new RecordingDocumentSaver("validated-candidate");
        var writer = new SafeFileWriter(
            saver,
            new PassThroughDocumentValidator(),
            "test-session");

        BridgeCommandException error;
        using (File.Open(target, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            error = Assert.Throws<BridgeCommandException>(
                () => writer.Save(target, overwrite: true));
        }

        var details = JsonSerializer.SerializeToElement(error.Details);
        Assert.Equal(BridgeErrorCodes.SaveFailed, error.Code);
        Assert.Equal("commit", details.GetProperty("phase").GetString());
        Assert.True(details.GetProperty("restored").GetBoolean());
        Assert.False(details.GetProperty("committed").GetBoolean());
        Assert.Equal("original", File.ReadAllText(target));
        Assert.Equal(target, saver.ReopenedPath);
    }

    private sealed class BusyDocumentSaver : IDocumentSaver
    {
        public void Save(string path) => throw new COMException(
            "busy",
            unchecked((int)0x8001010A));

        public void CloseForCommit()
        {
        }

        public void Reopen(string path)
        {
        }
    }

    private sealed class RecordingDocumentSaver : IDocumentSaver
    {
        private readonly string _contents;

        public RecordingDocumentSaver(string contents) => _contents = contents;

        public string? SavedPath { get; private set; }

        public bool ClosedForCommit { get; private set; }

        public string? ReopenedPath { get; private set; }

        public void Save(string path)
        {
            SavedPath = path;
            File.WriteAllText(path, _contents);
        }

        public void CloseForCommit() => ClosedForCommit = true;

        public void Reopen(string path) => ReopenedPath = path;
    }
}
