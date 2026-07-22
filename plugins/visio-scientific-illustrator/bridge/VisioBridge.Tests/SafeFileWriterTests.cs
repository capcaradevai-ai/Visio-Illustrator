using VisioBridge.Core;
using VisioBridge.Core.Files;
using VisioBridge.Tests.Fakes;

namespace VisioBridge.Tests;

public sealed class SafeFileWriterTests
{
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
}
