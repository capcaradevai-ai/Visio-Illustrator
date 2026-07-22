namespace VisioBridge.Tests.Fakes;

internal sealed class TestDirectory : IDisposable
{
    private TestDirectory(string path) => Path = path;

    public string Path { get; }

    public static TestDirectory Create()
    {
        var path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "visio-bridge-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return new TestDirectory(path);
    }

    public string WriteFile(string name, string contents)
    {
        var path = System.IO.Path.Combine(Path, name);
        File.WriteAllText(path, contents);
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true);
    }
}
