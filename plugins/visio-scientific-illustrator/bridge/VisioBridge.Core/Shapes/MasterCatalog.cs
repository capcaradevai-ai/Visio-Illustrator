using System.Runtime.InteropServices;
using VisioBridge.Core.Interop;
using Visio = Microsoft.Office.Interop.Visio;

namespace VisioBridge.Core.Shapes;

public sealed class MasterCatalog : IDisposable
{
    private static readonly IReadOnlyDictionary<string, string> Names =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["rounded_rectangle"] = "Codex Rounded Rectangle",
            ["diamond"] = "Codex Diamond",
            ["cylinder"] = "Codex Cylinder",
            ["hexagon"] = "Codex Hexagon",
            ["triangle"] = "Codex Triangle",
            ["parallelogram"] = "Codex Parallelogram",
            ["cloud"] = "Codex Cloud",
            ["container"] = "Codex Container",
        };

    private readonly Visio.InvisibleApp? _application;
    private readonly string? _stencilPath;
    private Visio.Document? _stencil;
    private bool _disposed;

    public MasterCatalog(Visio.InvisibleApp application, string stencilPath)
    {
        _application = application ?? throw new ArgumentNullException(nameof(application));
        _stencilPath = stencilPath;
    }

    private MasterCatalog()
    {
    }

    public static string GetMasterName(string shapeType)
    {
        if (!Names.TryGetValue(shapeType, out var name))
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.MasterNotFound,
                $"没有固定 Visio Master: {shapeType}");
        }
        return name;
    }

    public static bool IsKnown(string shapeType) => Names.ContainsKey(shapeType);

    internal Visio.Shape Drop(Visio.Page page, string shapeType, double pinX, double pinY)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var masterName = GetMasterName(shapeType);
        var stencil = EnsureOpen();
        var masters = stencil.Masters;
        Visio.Master? master = null;
        try
        {
            try
            {
                master = masters.ItemU[masterName];
            }
            catch (COMException error)
            {
                var translated = ComExceptionTranslator.Translate(error);
                if (translated.Code == BridgeErrorCodes.VisioBusy)
                {
                    throw translated;
                }
                throw new BridgeCommandException(
                    BridgeErrorCodes.MasterNotFound,
                    $"Stencil 缺少 Master NameU: {masterName}",
                    details: new { hresult = error.HResult });
            }
            return shapeType == "container"
                ? page.DropContainer(master, null!)
                : page.Drop(master, pinX, pinY);
        }
        finally
        {
            if (master is not null) Marshal.ReleaseComObject(master);
            Marshal.ReleaseComObject(masters);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_stencil is not null)
        {
            _stencil.Close();
            Marshal.ReleaseComObject(_stencil);
            _stencil = null;
        }
    }

    private Visio.Document EnsureOpen()
    {
        if (_stencil is not null) return _stencil;
        if (_application is null
            || string.IsNullOrWhiteSpace(_stencilPath)
            || !Path.IsPathFullyQualified(_stencilPath)
            || !File.Exists(_stencilPath))
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.MasterNotFound,
                $"找不到 bundled Visio stencil: {_stencilPath}");
        }
        var documents = _application.Documents;
        try
        {
            var flags = (short)(
                Visio.VisOpenSaveArgs.visOpenRO
                | Visio.VisOpenSaveArgs.visOpenHidden
                | Visio.VisOpenSaveArgs.visOpenDontList
                | Visio.VisOpenSaveArgs.visOpenMacrosDisabled);
            _stencil = documents.OpenEx(_stencilPath, flags);
            return _stencil;
        }
        finally
        {
            Marshal.ReleaseComObject(documents);
        }
    }
}
