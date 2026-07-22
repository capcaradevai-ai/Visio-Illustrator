namespace VisioBridge.Core.Abstractions;

public interface IDocumentSaver
{
    void Save(string absoluteVsDxPath);

    void CloseForCommit();

    void Reopen(string absoluteVsDxPath);
}
