using VisioTestBridge.Core.Abstractions;
using Visio = Microsoft.Office.Interop.Visio;

namespace VisioTestBridge.Core.Files;

internal sealed class VisioDocumentSaver : IDocumentSaver
{
    private readonly Func<object> _documentProvider;
    private readonly Action _closeForCommit;
    private readonly Action<string> _reopen;

    public VisioDocumentSaver(
        Func<object> documentProvider,
        Action closeForCommit,
        Action<string> reopen)
    {
        _documentProvider = documentProvider
            ?? throw new ArgumentNullException(nameof(documentProvider));
        _closeForCommit = closeForCommit
            ?? throw new ArgumentNullException(nameof(closeForCommit));
        _reopen = reopen ?? throw new ArgumentNullException(nameof(reopen));
    }

    public void Save(string absoluteVsDxPath)
    {
        var document = (Visio.Document)_documentProvider();
        var current = document.FullName;
        if (Path.IsPathFullyQualified(current)
            && string.Equals(
                Path.GetFullPath(current),
                Path.GetFullPath(absoluteVsDxPath),
                StringComparison.OrdinalIgnoreCase))
        {
            document.Save();
            return;
        }
        document.SaveAsEx(absoluteVsDxPath, 0);
    }

    public void CloseForCommit() => _closeForCommit();

    public void Reopen(string absoluteVsDxPath) => _reopen(absoluteVsDxPath);
}
