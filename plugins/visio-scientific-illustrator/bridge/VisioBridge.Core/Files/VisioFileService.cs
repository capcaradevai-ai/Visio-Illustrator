using VisioBridge.Contracts;

namespace VisioBridge.Core.Files;

internal sealed class VisioFileService
{
    private readonly SafeFileWriter _writer;

    public VisioFileService(SafeFileWriter writer) => _writer = writer;

    public SaveResult Save(SaveCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return _writer.Save(command.OutputPath, command.Overwrite);
    }
}
