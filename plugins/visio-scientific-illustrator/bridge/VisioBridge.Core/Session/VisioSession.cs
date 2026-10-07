using System.Globalization;
using System.Runtime.InteropServices;
using VisioBridge.Contracts;
using VisioBridge.Core.Abstractions;
using VisioBridge.Core.Geometry;
using VisioBridge.Core.Inspection;
using VisioBridge.Core.Layout;
using VisioBridge.Core.Scenes;
using VisioBridge.Core.Interop;
using VisioBridge.Core.Export;
using VisioBridge.Core.Files;
using VisioBridge.Core.Shapes;
using Visio = Microsoft.Office.Interop.Visio;

namespace VisioBridge.Core.Session;

public sealed class VisioSession : IVisioSession
{
    private readonly StaCommandDispatcher _dispatcher;
    private readonly string _sessionId = Guid.NewGuid().ToString("N");
    private readonly string _sessionTempRoot;
    private readonly string _pluginRoot;
    private Visio.InvisibleApp? _application;
    private Visio.Document? _document;
    private Visio.Page? _page;
    private CanvasSpec? _canvas;
    private MasterCatalog? _masterCatalog;
    private VisioShapeAccessor? _shapeAccessor;
    private ShapeRepository? _shapeRepository;
    private int? _boundPageId;
    private string? _boundPageNameU;
    private volatile SessionState _state = SessionState.Stopped;
    private bool _disposed;

    public VisioSession(string? pluginRoot = null)
    {
        _dispatcher = new StaCommandDispatcher();
        _pluginRoot = Path.GetFullPath(pluginRoot ?? Directory.GetCurrentDirectory());
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
            return CompleteLaunch(
                new CanvasSpec(widthPx, heightPx, dpi),
                maximize);
        });
    }

    public LaunchResult Open(
        string absoluteVsdxPath,
        string? pageName,
        int dpi,
        bool maximize)
    {
        ValidateOpenPath(absoluteVsdxPath);
        ValidateOpenOptions(pageName, dpi);
        EnsureCanLaunch();
        _state = SessionState.Starting;
        return InvokeLaunch(() =>
        {
            StartApplication();
            _document = OpenDocument(absoluteVsdxPath);
            _page = SelectPage(_document, pageName);
            return CompleteLaunch(ReadCanvas(_page, dpi), maximize);
        });
    }

    public SessionStatus GetStatus()
    {
        ThrowIfDisposed();
        if (_state == SessionState.Invalidated)
        {
            return new SessionStatus(
                SessionState.Invalidated,
                OwnedProcessId,
                false,
                null,
                null,
                0,
                0,
                false);
        }
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

        return InvokeSession(GetStatusCore);
    }

    public ShapeMutationResult AddShape(AddShapeCommand command)
        => ExecuteUndoable("Codex add shape", () => CreateShapeFactory().Add(command));

    public ShapeMutationResult AddImage(AddImageCommand command)
        => ExecuteUndoable("Codex add image", () => CreateShapeFactory().AddImage(command));

    public AdoptShapeResult AdoptShape(AdoptShapeCommand command)
        => ExecuteUndoable("Codex adopt existing shape", () =>
        {
            var repository = GetShapeRepository();
            using var shape = repository.Adopt(
                command.ShapeId,
                command.SemanticId);

            return new AdoptShapeResult(
                command.SemanticId,
                shape.Id,
                shape.NameU);
        });

    public PathMutationResult AddPath(AddPathCommand command)
        => ExecuteUndoable("Codex add path", () => CreateFreeformBuilder().Add(command));

    public PathMutationResult UpdatePath(UpdatePathCommand command)
        => ExecuteUndoable("Codex update path", () => CreateFreeformBuilder().Update(command));

    public ConnectorMutationResult AddConnector(AddConnectorCommand command)
        => ExecuteUndoable(
            "Codex add connector",
            () => CreateConnectorService().Add(command));

    public PageInspectionResult Inspect(int maxShapes)
        => InvokeReady(() =>
        {
            var result = CreateInspector().Inspect(maxShapes);
            GetShapeRepository().RebuildIndex();
            return result;
        });

    public ShapeSnapshot UpdateShape(UpdateShapeCommand command) =>
        ExecuteUndoable("Codex update shape", () => CreateShapeEditor().Update(command));

    public DeleteResult DeleteShape(DeleteShapeCommand command) =>
        ExecuteUndoable("Codex delete shape", () =>
        {
            var result = CreateShapeEditor().Delete(command);
            GetShapeRepository().RebuildIndex();
            return result;
        });

    public ClearResult Clear(bool confirm) =>
        ExecuteUndoable("Codex clear page", () =>
        {
            var result = CreateShapeEditor().Clear(confirm);
            GetShapeRepository().RebuildIndex();
            return result;
        });

    public PreviewResult ExportPreview(int widthPx)
        => InvokeReady(() => new PreviewExporter(
                new VisioPreviewExportAdapter(RequireReadyApplication(), _page!),
                _sessionTempRoot).Export(widthPx));

    public object Fit(string mode)
    {
        return InvokeReady<object>(() =>
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
        });
    }

    public RasterSettings ReadRasterSettingsForIntegrationTest()
        => InvokeReady(() => new VisioPreviewExportAdapter(
                RequireReadyApplication(),
                _page!).ReadSettings());

    public (string FullName, bool Saved) ReadDocumentSaveStateForIntegrationTest()
        => InvokeReady(() =>
        {
            RequireReadyApplication();
            return (_document!.FullName, _document.Saved);
        });

    public double ReadCellResultForIntegrationTest(string semanticId, string cellName)
        => InvokeReady(() =>
        {
            var repository = GetShapeRepository();
            using var handle = repository.Resolve(semanticId);
            var shape = ((VisioShapeHandle)handle).Shape;
            if (shape.CellExistsU[cellName, (short)Visio.VisExistsFlags.visExistsAnywhere] == 0)
                throw new BridgeCommandException(
                    BridgeErrorCodes.CellNotFound,
                    $"Shape {semanticId} 不存在单元格 {cellName}。");
            var cell = shape.CellsU[cellName];
            try
            {
                return cell.ResultIU;
            }
            finally
            {
                Marshal.FinalReleaseComObject(cell);
            }
        });

    public int ReadGroupChildCountForIntegrationTest(string semanticId)
        => InvokeReady(() =>
        {
            var repository = GetShapeRepository();
            using var handle = repository.Resolve(semanticId);
            var children = ((VisioShapeHandle)handle).Shape.Shapes;
            try { return children.Count; }
            finally { Marshal.FinalReleaseComObject(children); }
        });

    public SaveResult Save(SaveCommand command)
        => InvokeReady(() =>
        {
            RequireReadyApplication();
            var service = new VisioFileService(new SafeFileWriter(
                new VisioDocumentSaver(
                    () => (object)(_document ?? throw new BridgeCommandException(
                        BridgeErrorCodes.SessionNotReady,
                        "Visio 会话没有可保存的文档。")),
                    CloseValidatedDocumentForCommit,
                    ReopenDocumentAfterCommit),
                new VisioDocumentValidator(),
                _sessionId));
            return service.Save(command);
        });

    public GroupMutationResult GroupShapes(GroupShapesCommand command) =>
        ExecuteUndoable(
            "Codex group shapes",
            () => CreateGroupingService().Group(command));

    public UngroupMutationResult UngroupShape(UngroupShapeCommand command) =>
        ExecuteUndoable(
            "Codex ungroup shape",
            () =>
            {
                var result = CreateGroupingService().Ungroup(command);
                GetShapeRepository().RebuildIndex();
                return result;
            });

    public ContainerMutationResult AddToContainer(AddToContainerCommand command) =>
        ExecuteUndoable(
            "Codex add to container",
            () => CreateGroupingService().AddToContainer(command));

    public LayoutMutationResult SetZOrder(SetZOrderCommand command) =>
        ExecuteUndoable("Codex set z-order", () => CreateLayoutService().SetZOrder(command));

    public LayoutMutationResult AssignLayer(AssignLayerCommand command) =>
        ExecuteUndoable("Codex assign layer", () => CreateLayoutService().AssignLayer(command));

    public LayoutMutationResult AlignShapes(AlignShapesCommand command) =>
        ExecuteUndoable("Codex align shapes", () => CreateLayoutService().Align(command));

    public LayoutMutationResult DistributeShapes(DistributeShapesCommand command) =>
        ExecuteUndoable("Codex distribute shapes", () => CreateLayoutService().Distribute(command));

    public LayoutMutationResult MatchSize(MatchSizeCommand command) =>
        ExecuteUndoable("Codex match size", () => CreateLayoutService().MatchSize(command));

    public LayoutMutationResult SetProtection(SetProtectionCommand command) =>
        ExecuteUndoable("Codex set protection", () => CreateLayoutService().SetProtection(command));

    public LayoutMutationResult DuplicateShape(DuplicateShapeCommand command) =>
        ExecuteUndoable("Codex duplicate shape", () => CreateLayoutService().Duplicate(command));

    public ScientificComponentMutationResult AddScientificComponent(AddScientificComponentCommand command) =>
        ExecuteUndoable(
            "Codex add scientific component",
            () => CreateScientificComponentFactory().Add(command));

    public ShapeSheetCellsResult GetCells(GetCellsCommand command) =>
        InvokeReady(() => CreateShapeSheetService().GetCells(command));

    public ShapeSheetCellsResult SetCells(SetCellsCommand command) =>
        ExecuteUndoable("Codex set ShapeSheet cells", () => CreateShapeSheetService().SetCells(command));

    public ConnectionPointMutationResult AddConnectionPoint(AddConnectionPointCommand command) =>
        ExecuteUndoable("Codex add connection point", () => CreateShapeSheetService().AddConnectionPoint(command));

    public GeometryInspectionResult GetGeometry(GetGeometryCommand command) =>
        InvokeReady(() => CreateShapeSheetService().GetGeometry(command));

    public SceneExecutionPlan PrepareScene(DrawSceneCommand command) => InvokeReady(() =>
    {
        var existing = CreateInspector().Inspect(5000).Shapes
            .Select(shape => shape.SemanticId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToArray();
        return SceneExecutor.Prepare(command, existing);
    });

    public EnsureLayersResult EnsureLayers(EnsureLayersCommand command) =>
        ExecuteUndoable("Codex ensure scene layers", () => EnsureLayersCore(command));

    public object Refresh() => InvokeReady<object>(() =>
    {
        var window = RequireReadyApplication().ActiveWindow;
        try
        {
            window.Activate();
            return new { refreshed = true };
        }
        finally { ReleaseCom(ref window); }
    });

    public void UndoLastForIntegrationTest()
        => InvokeReady(() =>
        {
            RequireReadyApplication().Undo();
            _shapeRepository?.RebuildIndex();
        });

    public int AddPageForIntegrationTest(
        string pageName,
        int widthPx,
        int heightPx,
        int dpi,
        bool activate)
    {
        ValidateCanvas(pageName, widthPx, heightPx, dpi);
        return InvokeReady(() =>
        {
            var pages = _document!.Pages;
            Visio.Page? page = null;
            try
            {
                page = pages.Add();
                ConfigurePage(page, pageName, widthPx, heightPx, dpi);
                if (activate) DisplayPage(page);
                return page.ID;
            }
            finally
            {
                ReleaseCom(ref page);
                ReleaseCom(ref pages);
            }
        });
    }

    public (string BoundPage, string ActivePage) ReadPageBindingForIntegrationTest()
        => InvokeSession(() =>
        {
            RequireReadyApplication();
            var activePage = _application!.ActivePage;
            try
            {
                return (_page!.Name, activePage.Name);
            }
            finally
            {
                ReleaseComAlias(ref activePage);
            }
        });

    public void QuitApplicationExternallyForIntegrationTest()
        => InvokeReady(() =>
        {
            _document!.Saved = true;
            _application!.Quit();
        });

    public void AddConnectionPointForIntegrationTest(
        string semanticId,
        double xFraction,
        double yFraction)
        => InvokeReady(() =>
        {
            if (!double.IsFinite(xFraction)
                || !double.IsFinite(yFraction)
                || xFraction is < 0 or > 1
                || yFraction is < 0 or > 1)
            {
                throw new BridgeCommandException(
                    BridgeErrorCodes.InvalidArgument,
                    "测试连接点比例必须在 0–1 范围内。");
            }
            var repository = GetShapeRepository();
            using var handle = repository.Resolve(semanticId);
            var shape = ((VisioShapeHandle)handle).Shape;
            var section = (short)Visio.VisSectionIndices.visSectionConnectionPts;
            if (shape.SectionExists[section, 0] == 0) shape.AddSection(section);
            var row = shape.AddRow(
                section,
                (short)Visio.VisRowIndices.visRowLast,
                (short)Visio.VisRowTags.visTagCnnctPt);
            Visio.Cell? x = null;
            Visio.Cell? y = null;
            try
            {
                x = shape.CellsSRC[
                    section,
                    row,
                    (short)Visio.VisCellIndices.visCnnctX];
                y = shape.CellsSRC[
                    section,
                    row,
                    (short)Visio.VisCellIndices.visCnnctY];
                x.FormulaU = $"Width*{xFraction.ToString("0.######", CultureInfo.InvariantCulture)}";
                y.FormulaU = $"Height*{yFraction.ToString("0.######", CultureInfo.InvariantCulture)}";
            }
            finally
            {
                ReleaseCom(ref y);
                ReleaseCom(ref x);
            }
        });

    public void CloseDocument(string action)
    {
        ValidateDirtyAction(action);
        InvokeSession(() => CloseDocumentCore(action));
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

        if (_state == SessionState.Invalidated)
        {
            if (dirtyAction != "cancel")
            {
                _dispatcher.InvokeAsync(
                    () => ShutdownInvalidatedSession(dirtyAction))
                    .GetAwaiter()
                    .GetResult();
            }
            return;
        }

        try
        {
            InvokeSession(() =>
            {
                if (_document is not null)
                {
                    var cancelled = CloseDocumentCore(dirtyAction);
                    if (cancelled) return;
                }

                if (_application is not null)
                {
                    _masterCatalog?.Dispose();
                    _masterCatalog = null;
                    _application.Quit();
                    ReleaseCom(ref _application);
                }
                OwnedProcessId = null;
                _state = SessionState.Stopped;
            });
        }
        catch (BridgeCommandException error) when (
            error.Code == BridgeErrorCodes.SessionInvalidated
            && dirtyAction != "cancel")
        {
            _dispatcher.InvokeAsync(
                () => ShutdownInvalidatedSession(dirtyAction))
                .GetAwaiter()
                .GetResult();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _dispatcher.InvokeAsync(() =>
        {
            _masterCatalog?.Dispose();
            _masterCatalog = null;
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
            var translated = ComExceptionTranslator.Translate(error);
            if (translated.Code == BridgeErrorCodes.VisioBusy)
            {
                throw translated;
            }
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

    private void CloseValidatedDocumentForCommit()
    {
        if (_document is null || _page is null)
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.SessionNotReady,
                "Visio 会话没有可提交的文档。");
        }
        if (!_document.Saved)
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.SaveFailed,
                "暂存文档尚未完成保存，不能提交。");
        }

        _document.Close();
        ReleaseCom(ref _page);
        ReleaseCom(ref _document);
        _state = SessionState.Starting;
    }

    private void ReopenDocumentAfterCommit(string absoluteVsdxPath)
    {
        try
        {
            var pageId = _boundPageId ?? throw new BridgeCommandException(
                BridgeErrorCodes.SessionInvalidated,
                "安全保存前没有可恢复的绑定页 ID。");
            var dpi = _canvas?.Dpi ?? throw new BridgeCommandException(
                BridgeErrorCodes.SessionInvalidated,
                "安全保存前没有可恢复的画布 DPI。");
            _document = OpenDocument(absoluteVsdxPath);
            _page = SelectPageById(_document, pageId);
            _canvas = ReadCanvas(_page, dpi);
            ActivateBoundPage();
            _state = SessionState.Ready;
        }
        catch
        {
            ReleaseCom(ref _page);
            ReleaseCom(ref _document);
            _boundPageId = null;
            _boundPageNameU = null;
            _canvas = null;
            _state = SessionState.Invalidated;
            throw;
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

    private Visio.Page SelectPage(Visio.Document document, string? pageName)
    {
        if (string.IsNullOrWhiteSpace(pageName))
        {
            var activePage = _application!.ActivePage;
            return activePage ?? GetFirstPage(document);
        }

        var pages = document.Pages;
        try
        {
            for (var index = 1; index <= pages.Count; index++)
            {
                Visio.Page? page = pages[index];
                if (string.Equals(page.Name, pageName, StringComparison.Ordinal)
                    || string.Equals(page.NameU, pageName, StringComparison.Ordinal))
                {
                    return page;
                }
                ReleaseCom(ref page);
            }
        }
        finally
        {
            ReleaseCom(ref pages);
        }

        throw new BridgeCommandException(
            BridgeErrorCodes.PageNotFound,
            $"文档中不存在页面: {pageName}");
    }

    private Visio.Page SelectPageById(Visio.Document document, int pageId)
    {
        var pages = document.Pages;
        try
        {
            for (var index = 1; index <= pages.Count; index++)
            {
                Visio.Page? page = pages[index];
                if (page.ID == pageId) return page;
                ReleaseCom(ref page);
            }
        }
        finally
        {
            ReleaseCom(ref pages);
        }

        throw new BridgeCommandException(
            BridgeErrorCodes.PageNotFound,
            $"安全保存后找不到原绑定页 ID={pageId}，NameU={_boundPageNameU}。");
    }

    private static CanvasSpec ReadCanvas(Visio.Page page, int dpi)
    {
        var pageSheet = page.PageSheet;
        Visio.Cell? widthCell = null;
        Visio.Cell? heightCell = null;
        try
        {
            widthCell = pageSheet.CellsU["PageWidth"];
            heightCell = pageSheet.CellsU["PageHeight"];
            var widthPx = checked((int)Math.Round(
                widthCell.ResultIU * dpi,
                MidpointRounding.AwayFromZero));
            var heightPx = checked((int)Math.Round(
                heightCell.ResultIU * dpi,
                MidpointRounding.AwayFromZero));
            if (widthPx is < 100 or > 30000
                || heightPx is < 100 or > 30000)
            {
                throw new BridgeCommandException(
                    BridgeErrorCodes.InvalidCoordinates,
                    "所选页面在当前 DPI 下超出 100–30000 像素范围；请调整 dpi。");
            }
            return new CanvasSpec(widthPx, heightPx, dpi);
        }
        finally
        {
            ReleaseCom(ref heightCell);
            ReleaseCom(ref widthCell);
            ReleaseCom(ref pageSheet);
        }
    }

    private LaunchResult CompleteLaunch(
        CanvasSpec canvas,
        bool maximize)
    {
        _shapeAccessor = null;
        _shapeRepository = null;
        _boundPageId = _page!.ID;
        _boundPageNameU = _page.NameU;
        _canvas = canvas;
        _application!.Visible = true;
        ActivateBoundPage();
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
                    if (VisioShapeClassifier.IsConnector(shape)) connectorCount++;
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
        _shapeRepository = null;
        _shapeAccessor = null;
        ReleaseCom(ref _page);
        ReleaseCom(ref _document);
        _boundPageId = null;
        _boundPageNameU = null;
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
        var repository = GetShapeRepository();
        var geometry = new GeometryConverter(_canvas);
        return new ShapeFactory(
            _page,
            geometry,
            repository,
            GetMasterCatalog(),
            new PageInspector(_document!, _page, geometry));
    }

    private FreeformBuilder CreateFreeformBuilder()
    {
        RequireReadyApplication();
        var geometry = new GeometryConverter(_canvas!);
        return new FreeformBuilder(
            _page!,
            geometry,
            GetShapeRepository(),
            new PageInspector(_document!, _page!, geometry));
    }

    private ConnectorService CreateConnectorService()
    {
        if (_state != SessionState.Ready || _page is null)
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.SessionNotReady,
                "Visio 会话尚未准备好。");
        }
        var geometry = new GeometryConverter(_canvas!);
        return new ConnectorService(
            GetShapeRepository(),
            new VisioConnectorAdapter(_page, _application!, geometry));
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
        var repository = GetShapeRepository();
        var inspector = new PageInspector(_document!, _page!, geometry);
        return new ShapeEditor(new VisioShapeEditorAdapter(
            _page!,
            repository,
            geometry,
            inspector));
    }

    private MasterCatalog GetMasterCatalog()
    {
        RequireReadyApplication();
        return _masterCatalog ??= new MasterCatalog(
            _application!,
            Path.Combine(
                _pluginRoot,
                "assets",
                "visio-scientific-masters.vssx"));
    }

    private VisioGroupingService CreateGroupingService()
    {
        RequireReadyApplication();
        return new VisioGroupingService(
            _page!,
            GetShapeRepository());
    }

    private LayoutService CreateLayoutService()
    {
        RequireReadyApplication();
        var geometry = new GeometryConverter(_canvas!);
        var repository = GetShapeRepository();
        var inspector = new PageInspector(_document!, _page!, geometry);
        return new LayoutService(_page!, repository, geometry, inspector);
    }

    private ScientificComponentFactory CreateScientificComponentFactory()
    {
        RequireReadyApplication();
        var geometry = new GeometryConverter(_canvas!);
        var repository = GetShapeRepository();
        var inspector = new PageInspector(_document!, _page!, geometry);
        var shapeFactory = new ShapeFactory(
            _page!, geometry, repository, GetMasterCatalog(), inspector);
        var pathFactory = new FreeformBuilder(_page!, geometry, repository, inspector);
        var grouping = new VisioGroupingService(_page!, repository);
        return new ScientificComponentFactory(
            shapeFactory, pathFactory, grouping, repository, inspector);
    }

    private ShapeSheetService CreateShapeSheetService()
    {
        RequireReadyApplication();
        return new ShapeSheetService(GetShapeRepository());
    }

    private ShapeRepository GetShapeRepository()
    {
        RequireReadyApplication();
        _shapeAccessor ??= new VisioShapeAccessor(() => _page!);
        return _shapeRepository ??= new ShapeRepository(_shapeAccessor);
    }

    private EnsureLayersResult EnsureLayersCore(EnsureLayersCommand command)
    {
        if (command.Layers is null || command.Layers.Count is < 1 or > 128
            || command.Layers.Any(string.IsNullOrWhiteSpace)
            || command.Layers.Distinct(StringComparer.Ordinal).Count() != command.Layers.Count)
            throw new BridgeCommandException(BridgeErrorCodes.InvalidArgument, "layers 必须包含 1–128 个唯一非空名称。");
        var layers = _page!.Layers;
        var created = 0;
        try
        {
            var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 1; index <= layers.Count; index++)
            {
                var layer = layers[index];
                try { existing.Add(layer.NameU); existing.Add(layer.Name); }
                finally { Marshal.FinalReleaseComObject(layer); }
            }
            foreach (var name in command.Layers)
            {
                if (existing.Contains(name)) continue;
                var layer = layers.Add(name);
                try { created++; existing.Add(layer.NameU); }
                finally { Marshal.FinalReleaseComObject(layer); }
            }
            return new EnsureLayersResult(command.Layers, created);
        }
        finally { Marshal.FinalReleaseComObject(layers); }
    }

    private T ExecuteUndoable<T>(string name, Func<T> operation)
    {
        try
        {
            return InvokeReady(() =>
            {
                using var scope = new UndoScope(RequireReadyApplication(), name);
                var result = operation();
                scope.Complete();
                return result;
            });
        }
        catch (Exception operationError)
        {
            if (_state == SessionState.Ready && _shapeRepository is not null)
            {
                try
                {
                    InvokeReady(_shapeRepository.RebuildIndex);
                }
                catch (Exception rebuildError)
                {
                    throw new AggregateException(
                        "操作回滚后重建语义 ID 索引失败。",
                        operationError,
                        rebuildError);
                }
            }
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(operationError).Throw();
            throw new InvalidOperationException("不可达代码。");
        }
    }

    private Visio.InvisibleApp RequireReadyApplication()
    {
        if (_state == SessionState.Invalidated)
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.SessionInvalidated,
                "Visio 会话已失效，必须重新启动会话。");
        }
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

    private T InvokeReady<T>(Func<T> operation) => InvokeSession(() =>
    {
        RequireReadyApplication();
        ActivateBoundPage();
        return operation();
    });

    private void InvokeReady(Action operation) => InvokeReady(() =>
    {
        operation();
        return true;
    });

    private T InvokeSession<T>(Func<T> operation)
    {
        ThrowIfDisposed();
        try
        {
            return _dispatcher.InvokeAsync(operation).GetAwaiter().GetResult();
        }
        catch (BridgeCommandException)
        {
            throw;
        }
        catch (COMException error)
        {
            var translated = ComExceptionTranslator.Translate(error);
            if (translated.Code == BridgeErrorCodes.VisioBusy) throw translated;
            _state = SessionState.Invalidated;
            throw SessionInvalidated(error.Message, error.HResult);
        }
        catch (InvalidComObjectException error)
        {
            _state = SessionState.Invalidated;
            throw SessionInvalidated(error.Message, null);
        }
    }

    private void InvokeSession(Action operation) => InvokeSession(() =>
    {
        operation();
        return true;
    });

    private void ActivateBoundPage()
    {
        if (_application is null || _page is null || _boundPageId is null)
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.SessionInvalidated,
                "Visio 会话缺少绑定页。");
        }

        Visio.Window? window = null;
        object? windowPage = null;
        try
        {
            window = _application.ActiveWindow;
            if (window is null)
            {
                _state = SessionState.Invalidated;
                throw SessionInvalidated("Visio ActiveWindow 不存在。", null);
            }
            windowPage = window.Page;
            var visiblePage = windowPage as Visio.Page;
            var alreadyVisible = visiblePage is not null
                && visiblePage.ID == _boundPageId.Value;
            ReleaseComAlias(ref windowPage);
            if (!alreadyVisible)
            {
                window.Page = _page;
                window.Activate();
                windowPage = window.Page;
                visiblePage = windowPage as Visio.Page;
            }
            if (visiblePage is null || visiblePage.ID != _boundPageId.Value)
            {
                if (_state == SessionState.Ready) _state = SessionState.Invalidated;
                throw new BridgeCommandException(
                    _state == SessionState.Starting
                        ? BridgeErrorCodes.PageNotFound
                        : BridgeErrorCodes.SessionInvalidated,
                    $"Visio 可见窗口没有激活绑定页 ID={_boundPageId.Value}。");
            }
        }
        finally
        {
            ReleaseComAlias(ref windowPage);
            ReleaseCom(ref window);
        }
    }

    private void DisplayPage(Visio.Page page)
    {
        Visio.Window? window = null;
        try
        {
            window = _application!.ActiveWindow;
            window.Page = page;
            window.Activate();
        }
        finally
        {
            ReleaseCom(ref window);
        }
    }

    private static BridgeCommandException SessionInvalidated(
        string reason,
        int? hresult) => new(
            BridgeErrorCodes.SessionInvalidated,
            "Visio 会话已失效，必须重新启动会话。",
            details: new { reason, hresult });

    private void ShutdownInvalidatedSession(string dirtyAction)
    {
        _masterCatalog?.Dispose();
        _masterCatalog = null;
        if (dirtyAction == "save" && _document is null)
        {
            throw SessionInvalidated(
                "失效会话没有可确认保存成功的文档。",
                null);
        }
        try
        {
            if (_document is not null)
            {
                try
                {
                    CloseDocumentCore(dirtyAction);
                }
                catch (COMException) when (dirtyAction == "discard")
                {
                }
                catch (InvalidComObjectException) when (dirtyAction == "discard")
                {
                }
                catch (COMException error)
                {
                    _state = SessionState.Invalidated;
                    throw SessionInvalidated(error.Message, error.HResult);
                }
                catch (InvalidComObjectException error)
                {
                    _state = SessionState.Invalidated;
                    throw SessionInvalidated(error.Message, null);
                }
            }
            _application?.Quit();
        }
        catch (COMException)
        {
        }
        catch (InvalidComObjectException)
        {
        }
        ReleaseCom(ref _page);
        ReleaseCom(ref _document);
        ReleaseCom(ref _application);
        _boundPageId = null;
        _boundPageNameU = null;
        _canvas = null;
        OwnedProcessId = null;
        _state = SessionState.Stopped;
    }

    private static void ValidateCanvas(string pageName, int widthPx, int heightPx, int dpi)
    {
        if (string.IsNullOrWhiteSpace(pageName)
            || widthPx is < 100 or > 30000
            || heightPx is < 100 or > 30000
            || dpi is < 48 or > 600)
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.InvalidArgument,
                "pageName、widthPx、heightPx 和 dpi 必须有效且为正数。");
        }
    }

    private static void ValidateOpenOptions(string? pageName, int dpi)
    {
        if ((pageName is not null
                && (string.IsNullOrWhiteSpace(pageName) || pageName.Length > 255))
            || dpi is < 48 or > 600)
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.InvalidArgument,
                "page_name 必须为 1–255 个字符，dpi 必须在 48–600 范围内。");
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

    private static void ReleaseComAlias<T>(ref T? value)
        where T : class
    {
        if (value is not null && Marshal.IsComObject(value))
        {
            Marshal.ReleaseComObject(value);
        }
        value = null;
    }

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(
        IntPtr windowHandle,
        out uint processId);
}
