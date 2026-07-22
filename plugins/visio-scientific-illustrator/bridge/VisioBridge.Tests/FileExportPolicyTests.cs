using VisioBridge.Core;
using VisioBridge.Core.Files;
using System.Runtime.InteropServices;

namespace VisioBridge.Tests;

public sealed class FileExportPolicyTests
{
    [Fact]
    public void Partial_replace_failure_restores_existing_output_and_keeps_candidate()
    {
        using var temp = Fakes.TestDirectory.Create();
        var output = temp.WriteFile("figure.png", "original");
        var candidate = temp.WriteFile("candidate.png", "validated-candidate");
        var backup = Path.Combine(temp.Path, "figure.backup");
        var committer = new SafeOutputCommitter(new PartialFailureReplacer());

        var error = Assert.Throws<BridgeCommandException>(() =>
            committer.Commit(candidate, output, backup, existed: true));

        Assert.Equal(BridgeErrorCodes.ExportFailed, error.Code);
        Assert.Equal("original", File.ReadAllText(output));
        Assert.Equal("validated-candidate", File.ReadAllText(candidate));
        Assert.True(File.Exists(backup));
    }

    [Fact]
    public void Successful_overwrite_commits_exact_candidate_and_removes_backup()
    {
        using var temp = Fakes.TestDirectory.Create();
        var output = temp.WriteFile("figure.png", "original");
        var candidate = temp.WriteFile("candidate.png", "validated-candidate");
        var backup = Path.Combine(temp.Path, "figure.backup");

        new SafeOutputCommitter().Commit(candidate, output, backup, existed: true);

        Assert.Equal("validated-candidate", File.ReadAllText(output));
        Assert.False(File.Exists(candidate));
        Assert.False(File.Exists(backup));
    }

    [Fact]
    public void New_output_commit_never_deletes_a_file_created_after_preflight()
    {
        using var temp = Fakes.TestDirectory.Create();
        var candidate = temp.WriteFile("candidate.png", "validated-candidate");
        var output = temp.WriteFile("figure.png", "external-writer");
        var backup = Path.Combine(temp.Path, "figure.backup");

        var error = Assert.Throws<BridgeCommandException>(() =>
            new SafeOutputCommitter().Commit(candidate, output, backup, existed: false));

        Assert.Equal(BridgeErrorCodes.ExportFailed, error.Code);
        Assert.Equal("external-writer", File.ReadAllText(output));
        Assert.Equal("validated-candidate", File.ReadAllText(candidate));
        Assert.False(File.Exists(backup));
    }

    [Theory]
    [InlineData("png", "Page.Export")]
    [InlineData("svg", "Page.Export")]
    [InlineData("pdf", "Document.ExportAsFixedFormat")]
    public void Export_format_uses_expected_Visio_API(string format, string expected)
    {
        Assert.Equal(expected, FileExportPolicy.SelectApi(format));
    }

    [Fact]
    public void Unknown_export_format_is_rejected()
    {
        var error = Assert.Throws<BridgeCommandException>(
            () => FileExportPolicy.SelectApi("jpg"));

        Assert.Equal(BridgeErrorCodes.UnsupportedOperation, error.Code);
    }

    [Fact]
    public void File_mode_maps_class_not_registered_to_Visio_not_installed()
    {
        var error = VisioFileModeService.TranslateFileModeCom(
            new COMException("not registered", unchecked((int)0x80040154)),
            BridgeErrorCodes.ValidationFailed,
            "open_validate");

        Assert.Equal(BridgeErrorCodes.VisioNotInstalled, error.Code);
    }

    [Fact]
    public void File_mode_preserves_retryable_Visio_busy()
    {
        var error = VisioFileModeService.TranslateFileModeCom(
            new COMException("busy", unchecked((int)0x8001010A)),
            BridgeErrorCodes.ExportFailed,
            "export");

        Assert.Equal(BridgeErrorCodes.VisioBusy, error.Code);
        Assert.True(error.Retryable);
    }

    [Fact]
    public void File_mode_maps_open_failure_to_operation_specific_code()
    {
        var error = VisioFileModeService.TranslateFileModeCom(
            new COMException("damaged", unchecked((int)0x86DB08A5)),
            BridgeErrorCodes.ValidationFailed,
            "open_validate");

        Assert.Equal(BridgeErrorCodes.ValidationFailed, error.Code);
    }

    private sealed class PartialFailureReplacer : IOutputReplacer
    {
        public void Replace(string candidate, string output)
        {
            File.Delete(output);
            throw new IOException("injected partial replace failure");
        }
    }
}
