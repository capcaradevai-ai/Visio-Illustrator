using VisioBridge.Core.Abstractions;
using Visio = Microsoft.Office.Interop.Visio;

namespace VisioBridge.Core.Files;

internal sealed class VisioDocumentSaver : IDocumentSaver
{
    private readonly Visio.Document _document;

    public VisioDocumentSaver(Visio.Document document) => _document = document;

    public void Save(string absoluteVsDxPath)
    {
        var current = _document.FullName;
        if (Path.IsPathFullyQualified(current)
            && string.Equals(
                Path.GetFullPath(current),
                Path.GetFullPath(absoluteVsDxPath),
                StringComparison.OrdinalIgnoreCase))
        {
            _document.Save();
            return;
        }
        _document.SaveAsEx(absoluteVsDxPath, 0);
    }
}
