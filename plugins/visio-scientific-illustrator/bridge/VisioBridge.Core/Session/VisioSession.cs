using System.Globalization;
using System.Runtime.InteropServices;
using VisioBridge.Contracts;
using VisioBridge.Core.Abstractions;
using VisioBridge.Core.Geometry;
using VisioBridge.Core.Inspection;
using VisioBridge.Core.Export;
using VisioBridge.Core.Shapes;
using Visio = Microsoft.Office.Interop.Visio;

namespace VisioBridge.Core.Session;

public sealed class VisioSession : IVisioSession
{
    private readonly StaCommandDispatcher _dispatcher;
    private readonly string _sessionId = Guid.NewGuid().ToString("N");
    private readonly string _sessionTempRoot;
    private Visio.InvisibleApp? _application;
    private Visio.Document? _document;
    private Visio.Page? _page;
    private CanvasSpec? _canvas;
    private int? _boundPageId;
    private volatile SessionState _state = SessionState.Stopped;
    private bool _disposed;

    public VisioSession()
    {
        _dispatcher = new StaCommandDispatcher();
        _sessionTempRoot = Path.Combine(
            Path.GetTempPath(),
            "visio-scientific-illustrator",
            _sessionId);
    }

    public SessionState State => _state;

    public int? OwnedProcessId { get; private set; }

    public LaunchResult LaunchNew(
        string pageName,
        int widthPx,
        int heightPx,
        int dpi,
        bool maximize)
    {
        ValidateCanvas(pageName, widthPx, heightPx, dpi);
        EnsureCanLaunch();
        _state = SessionState.Starting;
        return InvokeLaunch(() =>
        {
            StartApplication();
            _document = AddDocument();
            _page = GetFirstPage(_document);
            ConfigurePage(_page, pageName, widthPx, heightPx, dpi);
            return CompleteLaunch(widthPx, heightPx, dpi, maximize);
        });
    }

    public LaunchResult Open(
        string absoluteVsdxPath,
        int widthPx,
        int heightPx,
        int dpi,
        bool maximize)
    {
        ValidateOpenPath(absoluteVsdxPath);
        ValidateCanvas("Figure", widthPx, heightPx, dpi);
        EnsureCanLaunch();
        _state = SessionState.Starting;
        return InvokeLaunch(() =>
        {
            StartApplication();
            _document = OpenDocument(absoluteVsdxPath);
            _page = _application!.ActivePage;
            if (_page is null) _page = GetFirstPage(_document);
            return CompleteLaunch(widthPx, heightPx, dpi, maximize);
        });
    }

    public SessionStatus GetStatus()
    {
        ThrowIfDisposed();
        if (_application is null)
        {
            return new SessionStatus(
                _state,
                OwnedProcessId,
                false,
                null,
                null,
                0,
                0,
                false);
        }

        try
        {
            return _dispatcher.InvokeAsync(GetStatusCore).GetAwaiter().GetResult();
        }
        catch (COMException error)
        {
            _state = SessionState.Invalidated;
            throw new BridgeCommandException(
                BridgeErrorCodes.SessionInvalidated,
                "Visio 会话已失效。",
                details: new { hresult = error.HResult });
        }
    }

    public ShapeMutationResult AddShape(AddShapeCommand command)
        => ExecuteUndoable("Codex add shape", () => CreateShapeFactory().Add(command));

    public ShapeMutationResult AddImage(AddImageCommand command)
        => ExecuteUndoable("Codex add image", () => CreateShapeFactory().AddImage(command));

    public ConnectorMutationResult AddConnector(AddConnectorCommand command)
        => ExecuteUndoable(
            "Codex add connector",
            () => CreateConnectorService().Add(command));

    public PageInspectionResult Inspect(int maxShapes)
    {
        ThrowIfDisposed();
        return _dispatcher.InvokeAsync(() => CreateInspector().Inspect(maxShapes))
            .GetAwaiter()
            .GetResult();
    }

    public ShapeSnapshot UpdateShape(UpdateShapeCommand command) =>
        ExecuteUndoable("Codex update shape", () => CreateShapeEditor().Update(command));

    public DeleteResult DeleteShape(DeleteShapeCommand command) =>
        ExecuteUndoable("Codex delete shape", () => CreateShapeEditor().Delete(command));

    public ClearResult Clear(bool confirm) =>
        ExecuteUndoable("Codex clear page", () => CreateShapeEditor().Clear(confirm));

    public PreviewResult ExportPreview(int widthPx)
    {
        ThrowIfDisposed();
        return _dispatcher.InvokeAsync(() => new PreviewExporter(
                new VisioPreviewExportAdapter(RequireReadyApplication(), _page!),
                _sessionTempRoot).Export(widthPx))
            .GetAwaiter()
            .GetResult();
    }

    public object Fit(string mode)
    {
        ThrowIfDisposed();
        return _dispatcher.InvokeAsync<object>(() =>
        {
            var application = RequireReadyApplication();
            if (mode is not ("page" or "width"))
            {
                throw new BridgeCommandException(
                    BridgeErrorCodes.InvalidArgument,
                    "fit mode 仅允许 page 或 width。");
            }
            var window = application.ActiveWindow;
            try
            {
                if (window.Type != (short)Visio.VisWinTypes.visDrawing)
                {
                    throw BridgeCommandException.Unsupported(
                        "当前 Visio 窗口不是绘图窗口。");
                }
                window.ViewFit = mode == "page"
                    ? (int)Visio.VisWindowFit.visFitPage
                    : (int)Visio.VisWindowFit.visFitWidth;
                return new { mode };
            }
            finally
            {
                ReleaseCom(ref window);
            }
        }).GetAwaiter().GetResult();
    }

    public RasterSettings ReadRasterSettingsForIntegrationTest()
    {
        ThrowIfDisposed();
        return _dispatcher.InvokeAsync(() => new VisioPreviewExportAdapter(
                RequireReadyApplication(),
                _page!).ReadSettings())
            .GetAwaiter()
            .GetResult();
    }

    public void UndoLastForIntegrationTest()
    {
        ThrowIfDisposed();
        _dispatcher.InvokeAsync(() => RequireReadyApplication().Undo())
            .GetAwaiter()
            .GetResult();
    }

    public void CloseDocument(string action)
    {
        ThrowIfDisposed();
        ValidateDirtyAction(action);
        _dispatcher.InvokeAsync(() => CloseDocumentCore(action)).GetAwaiter().GetResult();
    }

    public void Shutdown(bool confirm, string dirtyAction)
    {
        ThrowIfDisposed();
        if (!confirm)
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.InvalidArgument,
                "shutdown 要求 confirm=true。");
        }
        ValidateDirtyAction(dirtyAction);

        _dispatcher.InvokeAsync(() =>
        {
            if (_document is not null)
            {
                var cancelled = CloseDocumentCore(dirtyAction);
                if (cancelled) return;
            }

            if (_application is not null)
            {
                _application.Quit();
                ReleaseCom(ref _application);
            }
            OwnedProcessId = null;
            _state = SessionState.Stopped;
        }).GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _dispatcher.InvokeAsync(() =>
        {
            ReleaseCom(ref _page);
            ReleaseCom(ref _document);
            ReleaseCom(ref _application);
        }).GetAwaiter().GetResult();
        _dispatcher.Dispose();
    }

    private LaunchResult InvokeLaunch(Func<LaunchResult> launch)
    {
        try
        {
            return _dispatcher.InvokeAsync(launch).GetAwaiter().GetResult();
        }
        catch (COMException error)
        {
            _state = SessionState.Faulted;
            TryQuitAfterLaunchFailure();
            var code = error.HResult == unchecked((int)0x80040154)
                ? BridgeErrorCodes.VisioNotInstalled
                : BridgeErrorCodes.VisioStartFailed;
            throw new BridgeCommandException(
                code,
                "无法启动 Microsoft Visio 会话。",
                details: new { hresult = error.HResult });
        }
        catch
        {
            _state = SessionState.Faulted;
            TryQuitAfterLaunchFailure();
            throw;
        }
    }

    private void StartApplication()
    {
        Directory.CreateDirectory(_sessionTempRoot);
        _application = new Visio.InvisibleApp();
        var windowHandle = new IntPtr(_application.WindowHandle32);
        if (windowHandle == IntPtr.Zero
            || GetWindowThreadProcessId(windowHandle, out var processId) == 0
            || processId == 0)
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.VisioStartFailed,
                "无法从 Visio 主窗口句柄解析 owned Windows PID。");
        }
        OwnedProcessId = checked((int)processId);
    }

    private Visio.Document AddDocument()
    {
        var documents = _application!.Documents;
        try
        {
            return documents.Add("");
        }
        finally
        {
            ReleaseCom(ref documents);
        }
    }

    private Visio.Document OpenDocument(string absoluteVsdxPath)
    {
        var documents = _application!.Documents;
        try
        {
            return documents.Open(absoluteVsdxPath);
        }
        finally
        {
            ReleaseCom(ref documents);
        }
    }

    private static Visio.Page GetFirstPage(Visio.Document document)
    {
        var pages = document.Pages;
        try
        {
            return pages[1];
        }
        finally
        {
            ReleaseCom(ref pages);
        }
    }

    private LaunchResult CompleteLaunch(
        int widthPx,
        int heightPx,
        int dpi,
        bool maximize)
    {
        _boundPageId = _page!.ID;
        _canvas = new CanvasSpec(widthPx, heightPx, dpi);
        _application!.Visible = true;
        if (maximize)
        {
            var window = _application.ActiveWindow;
            try
            {
                window.WindowState = (int)Visio.VisWindowStates.visWSMaximized;
            }
            finally
            {
                ReleaseCom(ref window);
            }
        }

        _state = SessionState.Ready;
        return new LaunchResult(
            _sessionId,
            OwnedProcessId!.Value,
            _application.Visible,
            _application.Version,
            _document!.Name,
            _page.Name,
            _canvas!,
            _sessionTempRoot);
    }

    private SessionStatus GetStatusCore()
    {
        if (_document is null || _page is null)
        {
            return new SessionStatus(
                _state,
                OwnedProcessId,
                _application?.Visible ?? false,
                null,
                null,
                0,
                0,
                false);
        }

        var shapeCount = 0;
        var connectorCount = 0;
        var shapes = _page.Shapes;
        try
        {
            for (var index = 1; index <= shapes.Count; index++)
            {
                var shape = shapes[index];
                try
                {
                    if (shape.OneD != 0) connectorCount++;
                    else shapeCount++;
                }
                finally
                {
                    ReleaseCom(ref shape);
                }
            }
        }
        finally
        {
            ReleaseCom(ref shapes);
        }

        return new SessionStatus(
            _state,
            OwnedProcessId,
            _application!.Visible,
            _document.Name,
            _page.Name,
            shapeCount,
            connectorCount,
            !_document.Saved);
    }

    private bool CloseDocumentCore(string action)
    {
        if (_document is null)
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.DocumentNotFound,
                "当前会话没有打开的 Visio 文档。");
        }
        if (action == "cancel") return true;

        if (action == "save")
        {
            var fullName = _document.FullName;
            if (!Path.IsPathFullyQualified(fullName)
                || !string.Equals(Path.GetExtension(fullName), ".vsdx", StringComparison.OrdinalIgnoreCase))
            {
                throw new BridgeCommandException(
                    BridgeErrorCodes.SavePathRequired,
                    "未保存文档必须先通过 visio_live_save 指定绝对 .vsdx 路径。");
            }
            _document.Save();
        }
        else
        {
            _document.Saved = true;
        }

        _document.Close();
        ReleaseCom(ref _page);
        ReleaseCom(ref _document);
        _boundPageId = null;
        _canvas = null;
        _state = SessionState.Stopped;
        return false;
    }

    private static void ConfigurePage(
        Visio.Page page,
        string pageName,
        int widthPx,
        int heightPx,
        int dpi)
    {
        page.Name = pageName;
        var pageSheet = page.PageSheet;
        Visio.Cell? widthCell = null;
        Visio.Cell? heightCell = null;
        try
        {
            widthCell = pageSheet.CellsU["PageWidth"];
            heightCell = pageSheet.CellsU["PageHeight"];
            var widthInches = ((double)widthPx / dpi).ToString(
                "0.######",
                CultureInfo.InvariantCulture);
            var heightInches = ((double)heightPx / dpi).ToString(
                "0.######",
                CultureInfo.InvariantCulture);
            widthCell.FormulaU = $"{widthInches} in";
            heightCell.FormulaU = $"{heightInches} in";
        }
        finally
        {
            ReleaseCom(ref heightCell);
            ReleaseCom(ref widthCell);
            ReleaseCom(ref pageSheet);
        }
    }

    private void TryQuitAfterLaunchFailure()
    {
        _dispatcher.InvokeAsync(() =>
        {
            try
            {
                _document?.Close();
                _application?.Quit();
            }
            finally
            {
                ReleaseCom(ref _page);
                ReleaseCom(ref _document);
                ReleaseCom(ref _application);
                OwnedProcessId = null;
            }
        }).GetAwaiter().GetResult();
    }

    private void EnsureCanLaunch()
    {
        ThrowIfDisposed();
        if (_application is not null || _state is SessionState.Starting or SessionState.Ready)
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.InvalidArgument,
                "当前会话已经启动。");
        }
    }

    private ShapeFactory CreateShapeFactory()
    {
        if (_state != SessionState.Ready || _page is null || _canvas is null)
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.SessionNotReady,
                "Visio 会话尚未准备好。");
        }
        var accessor = new VisioShapeAccessor(() => _page!);
        var repository = new ShapeRepository(accessor);
        return new ShapeFactory(
            _page,
            new GeometryConverter(_canvas),
            repository);
    }

    private ConnectorService CreateConnectorService()
    {
        if (_state != SessionState.Ready || _page is null)
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.SessionNotReady,
                "Visio 会话尚未准备好。");
        }
        var accessor = new VisioShapeAccessor(() => _page!);
        return new ConnectorService(
            new ShapeRepository(accessor),
            new VisioConnectorAdapter(_page, _application!));
    }

    private PageInspector CreateInspector()
    {
        RequireReadyApplication();
        return new PageInspector(
            _document!,
            _page!,
            new GeometryConverter(_canvas!));
    }

    private ShapeEditor CreateShapeEditor()
    {
        RequireReadyApplication();
        var geometry = new GeometryConverter(_canvas!);
        var accessor = new VisioShapeAccessor(() => _page!);
        var repository = new ShapeRepository(accessor);
        var inspector = new PageInspector(_document!, _page!, geometry);
        return new ShapeEditor(new VisioShapeEditorAdapter(
            _page!,
            repository,
            geometry,
            inspector));
    }

    private T ExecuteUndoable<T>(string name, Func<T> operation)
    {
        ThrowIfDisposed();
        return _dispatcher.InvokeAsync(() =>
        {
            using var scope = new UndoScope(RequireReadyApplication(), name);
            var result = operation();
            scope.Complete();
            return result;
        }).GetAwaiter().GetResult();
    }

    private Visio.InvisibleApp RequireReadyApplication()
    {
        if (_state != SessionState.Ready
            || _application is null
            || _document is null
            || _page is null
            || _canvas is null)
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.SessionNotReady,
                "Visio 会话尚未准备好。");
        }
        return _application;
    }

    private static void ValidateCanvas(string pageName, int widthPx, int heightPx, int dpi)
    {
        if (string.IsNullOrWhiteSpace(pageName)
            || widthPx <= 0
            || heightPx <= 0
            || dpi <= 0)
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.InvalidArgument,
                "pageName、widthPx、heightPx 和 dpi 必须有效且为正数。");
        }
    }

    private static void ValidateOpenPath(string path)
    {
        if (!Path.IsPathFullyQualified(path)
            || !string.Equals(Path.GetExtension(path), ".vsdx", StringComparison.OrdinalIgnoreCase)
            || !File.Exists(path))
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.InvalidArgument,
                "file_path 必须是已存在的绝对 .vsdx 路径。");
        }
    }

    private static void ValidateDirtyAction(string action)
    {
        if (action is not ("save" or "discard" or "cancel"))
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.InvalidArgument,
                "action 仅允许 save、discard 或 cancel。");
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    private static void ReleaseCom<T>(ref T? value)
        where T : class
    {
        if (value is not null && Marshal.IsComObject(value))
        {
            Marshal.FinalReleaseComObject(value);
        }
        value = null;
    }

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(
        IntPtr windowHandle,
        out uint processId);
}
