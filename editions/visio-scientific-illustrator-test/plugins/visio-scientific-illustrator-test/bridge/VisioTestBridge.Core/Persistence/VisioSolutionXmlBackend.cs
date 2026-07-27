using Visio = Microsoft.Office.Interop.Visio;

namespace VisioTestBridge.Core.Persistence;

public sealed class VisioSolutionXmlBackend(Visio.Document document) : ISolutionXmlBackend
{
    private readonly Visio.IVDocument _document = document;

    public string? Read(string solutionName) =>
        _document.get_SolutionXMLElementExists(solutionName)
            ? _document.get_SolutionXMLElement(solutionName)
            : null;

    public void Write(string solutionName, string xml) =>
        _document.set_SolutionXMLElement(solutionName, xml);
}
