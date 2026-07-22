using VisioBridge.Core.Abstractions;

namespace VisioBridge.Tests.Fakes;

internal sealed class ThrowingDocumentSaver : IDocumentSaver
{
    public void Save(string path) => throw new IOException("injected save failure");
}

internal sealed class PassThroughDocumentValidator : IDocumentValidator
{
    public void Validate(string path)
    {
    }
}
