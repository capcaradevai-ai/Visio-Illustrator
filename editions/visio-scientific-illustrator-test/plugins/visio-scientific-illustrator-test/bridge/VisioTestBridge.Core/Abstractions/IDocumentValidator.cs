namespace VisioTestBridge.Core.Abstractions;

public interface IDocumentValidator
{
    void Validate(string absoluteVsDxPath);
}
