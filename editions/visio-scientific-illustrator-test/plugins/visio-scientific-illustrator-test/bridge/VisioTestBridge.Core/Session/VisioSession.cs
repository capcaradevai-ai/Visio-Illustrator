using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using VisioTestBridge.Contracts;
using VisioTestBridge.Core.Abstractions;
using VisioTestBridge.Core.Capabilities;
using VisioTestBridge.Core.Components;
using VisioTestBridge.Core.Geometry;
using VisioTestBridge.Core.Inspection;
using VisioTestBridge.Core.Layout;
using VisioTestBridge.Core.Scenes;
using VisioTestBridge.Core.Interop;
using VisioTestBridge.Core.Export;
using VisioTestBridge.Core.Files;
using VisioTestBridge.Core.Persistence;
using VisioTestBridge.Core.Quality;
using VisioTestBridge.Core.Shapes;
using Visio = Microsoft.Office.Interop.Visio;

namespace VisioTestBridge.Core.Session;

public sealed class VisioSession : IVisioSession
{
    private readonly StaCommandDispatcher _dispatcher;
    private readonly string _sessionId = Guid.NewGuid().ToString("N");
    private readonly string _sessionTempRoot;
    private readonly string _pluginRoot;
    private readonly CapabilityService _capabilityService = new();
    private Visio.InvisibleApp? _application;
    private Visio.Document? _document;
    private Visio.Page? _page;
    private CanvasSpec? _canvas;
    private MasterCatalog? _masterCatalog;
    private VisioShapeAccessor? _shapeAccessor;
    private ShapeRepository? _shapeRepository;
    private int? _boundPageId;
    private string? _boundPageNameU;
    private long _pageRevision;
    private RegionQualityState _qualityState = RegionQualityState.Planned;
    private string _currentRegion = "page";
    private long? _lastPassedRevision;
    private string _previewSha256 = new('0', 64);
    private string _structureSha256 = new('0', 64);
    private string _solutionXmlSha256 = new('0', 64);
    private readonly Dictionary<string, AuditFinding> _auditFindings =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, PersistedFindingState> _persistedFindingStates =
        new(StringComparer.Ordinal);
    private readonly HashSet<string> _registeredRegionIds = new(StringComparer.Ordinal);
    private readonly List<PersistedAuditEntry> _regionAuditHistory = [];
    private readonly List<PersistedAuditEntry> _wholePageAuditHistory = [];
    private volatile SessionState _state = SessionState.Stopped;
    private bool _disposed;

    public VisioSession(string? pluginRoot = null)
    {
        _dispatcher = new StaCommandDispatcher();
        _pluginRoot = Path.GetFullPath(pluginRoot ?? Directory.GetCurrentDirectory());
        _sessionTempRoot = Path.Combine(
            Path.GetTempPath(),
            "visio-scientific-illustrator-test",
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

    public CapabilitySummary GetCapabilities()
    {
        ThrowIfDisposed();
        var snapshot = _state == SessionState.Ready && _application is not null
            ? InvokeSession(CreateCapabilitySnapshot)
            : _capabilityService.ProbeInstalled(_pluginRoot);
        return _capabilityService.GetCapabilities(snapshot);
    }

    public CapabilityDetailsResult GetCapabilityDetails(GetCapabilityDetailsCommand command)
    {
        ThrowIfDisposed();
        var snapshot = _state == SessionState.Ready && _application is not null
            ? InvokeSession(CreateCapabilitySnapshot)
            : _capabilityService.ProbeInstalled(_pluginRoot);
        return _capabilityService.GetCapabilityDetails(snapshot, command);
    }

    public ShapeMutationResult AddShape(AddShapeCommand command)
        => ExecuteUndoable("Codex add shape", () => CreateShapeFactory().Add(command));

    public ShapeMutationResult AddImage(AddImageCommand command)
        => ExecuteUndoable("Codex add image", () => CreateShapeFactory().AddImage(command));

    public PathMutationResult AddPath(AddPathCommand command)
        => ExecuteUndoable("Codex add path", () => CreateFreeformBuilder().Add(command));

    public PathMutationResult UpdatePath(UpdatePathCommand command)
        => ExecuteUndoable("Codex update path", () => CreateFreeformBuilder().Update(command));

    public ConnectorMutationResult AddConnector(AddConnectorCommand command)
        => ExecuteUndoable(
            "Codex add connector",
            () => CreateConnectorService().Add(command));

    public CompositeComponentMutationResult AddTable(TableComponentContract command)
        => ExecuteUndoable("Codex add editable table", () => ExecuteComposite(
            command.Component,
            TableComponentPlanner.CreatePlan(command)));

    public CompositeComponentMutationResult AddChart(ChartComponentContract command)
        => ExecuteUndoable("Codex add editable chart", () => ExecuteComposite(
            command.Component,
            ChartComponentPlanner.CreatePlan(command)));

    public CompositeComponentMutationResult AddMatrix(MatrixComponentContract command)
        => ExecuteUndoable("Codex add editable matrix", () => ExecuteComposite(
            command.Component,
            MatrixComponentPlanner.CreatePlan(command)));

    public CompositeComponentMutationResult AddGrid(GridComponentContract command)
        => ExecuteUndoable("Codex add editable grid", () => ExecuteComposite(
            command.Component,
            GridComponentPlanner.CreatePlan(command)));

    public ComponentUpdateResult UpdateTableCell(UpdateTableCellCommand command)
        => ExecuteUndoable("Codex update table cell", () =>
        {
            RequireRevision(command.ExpectedRevision);
            if (command.Row < 0 || command.Column < 0 || command.Cell is null)
                throw new BridgeCommandException(
                    BridgeErrorCodes.ComponentContractInvalid,
                    "表格单元格坐标或内容无效。");
            var childId = $"{command.SemanticId}.cell.r{command.Row}.c{command.Column}";
            var style = new ShapeStyle(
                FillColor: command.Cell.Fill,
                FontColor: command.Cell.TextColor,
                FontSizePt: command.Cell.FontSize);
            CreateShapeEditor().Update(new UpdateShapeCommand(
                childId,
                Label: command.Cell.Text,
                Style: style));
            return ComponentUpdate(command.SemanticId, [childId]);
        });

    public ComponentUpdateResult UpdateTableLayout(UpdateTableLayoutCommand command)
        => ExecuteUndoable("Codex update table layout", () =>
        {
            RequireRevision(command.ExpectedRevision);
            ArgumentNullException.ThrowIfNull(command.Layout);
            var inspection = CreateInspector().Inspect(5000);
            var parent = inspection.Shapes.FirstOrDefault(item =>
                string.Equals(item.SemanticId, command.SemanticId, StringComparison.Ordinal))
                ?? throw new BridgeCommandException(
                    BridgeErrorCodes.ShapeNotFound,
                    $"不存在表格：{command.SemanticId}");
            var pattern = new Regex(
                $"^{Regex.Escape(command.SemanticId)}\\.cell\\.r(?<row>\\d+)\\.c(?<column>\\d+)$",
                RegexOptions.CultureInvariant);
            var cells = inspection.Shapes
                .Select(item => (Shape: item, Match: pattern.Match(item.SemanticId)))
                .Where(item => item.Match.Success)
                .Select(item => (
                    item.Shape,
                    Row: int.Parse(item.Match.Groups["row"].Value, CultureInfo.InvariantCulture),
                    Column: int.Parse(item.Match.Groups["column"].Value, CultureInfo.InvariantCulture)))
                .ToArray();
            if (cells.Length == 0)
                throw new BridgeCommandException(
                    BridgeErrorCodes.ComponentContractInvalid,
                    "表格不包含可识别的独立单元格。");
            var rows = cells.Max(item => item.Row) + 1;
            var columns = cells.Max(item => item.Column) + 1;
            var heights = ResolveComponentDimensions(
                command.Layout.RowHeights,
                rows,
                parent.Bounds.Height,
                "row_heights");
            var widths = ResolveComponentDimensions(
                command.Layout.ColumnWidths,
                columns,
                parent.Bounds.Width,
                "column_widths");
            var editor = CreateShapeEditor();
            foreach (var cell in cells)
            {
                editor.Update(new UpdateShapeCommand(
                    cell.Shape.SemanticId,
                    X: parent.Bounds.X + widths.Take(cell.Column).Sum(),
                    Y: parent.Bounds.Y + heights.Take(cell.Row).Sum(),
                    Width: widths[cell.Column],
                    Height: heights[cell.Row]));
            }
            return ComponentUpdate(
                command.SemanticId,
                cells.Select(item => item.Shape.SemanticId).ToArray());
        });

    public CompositeComponentMutationResult UpdateChart(ChartComponentContract command)
        => ExecuteUndoable("Codex update editable chart", () =>
            ReplaceComposite(
                command.Component,
                ChartComponentPlanner.CreatePlan(command)));

    public ComponentUpdateResult UpdateMatrixCell(UpdateMatrixCellCommand command)
        => ExecuteUndoable("Codex update matrix cell", () =>
        {
            RequireRevision(command.ExpectedRevision);
            if (command.Row < 0 || command.Column < 0 || !double.IsFinite(command.Value))
                throw new BridgeCommandException(
                    BridgeErrorCodes.ComponentContractInvalid,
                    "矩阵单元格坐标或数值无效。");
            var childId = $"{command.SemanticId}.cell.r{command.Row}.c{command.Column}";
            CreateShapeEditor().Update(new UpdateShapeCommand(
                childId,
                Label: command.Value.ToString("0.###", CultureInfo.InvariantCulture),
                Style: command.Fill is null
                    ? null
                    : new ShapeStyle(FillColor: command.Fill)));
            return ComponentUpdate(command.SemanticId, [childId]);
        });

    public CompositeComponentMutationResult UpdateGrid(GridComponentContract command)
        => ExecuteUndoable("Codex update editable grid", () =>
            ReplaceComposite(
                command.Component,
                GridComponentPlanner.CreatePlan(command)));

    public FigureAuditResult AuditFigure(FigureAuditInput command)
        => InvokeReady(() =>
        {
            ArgumentNullException.ThrowIfNull(command);
            InvalidateExternalPassIfNeeded();
            if (command.PageRevision != _pageRevision
                || command.Evidence.PageRevision != _pageRevision)
            {
                throw new BridgeCommandException(
                    BridgeErrorCodes.StaleAuditEvidence,
                    "审计输入不是当前页面修订版。",
                    retryable: true,
                    details: new
                    {
                        observed_revision = command.PageRevision,
                        evidence_revision = command.Evidence.PageRevision,
                        current_revision = _pageRevision,
                    });
            }

            var currentStructure = ComputeStructureHashCore();
            if (!string.Equals(
                    command.Evidence.StructureSnapshotSha256,
                    currentStructure.StructureSnapshotSha256,
                    StringComparison.Ordinal))
            {
                throw new BridgeCommandException(
                    BridgeErrorCodes.StaleAuditEvidence,
                    "审计结构哈希与当前 Visio 页面不一致。",
                    retryable: true,
                    details: new
                    {
                        observed_structure_sha256 =
                            command.Evidence.StructureSnapshotSha256,
                        current_structure_sha256 =
                            currentStructure.StructureSnapshotSha256,
                    });
            }
            var observedCommand = ObserveAuditInput(command);
            var result = DeterministicFigureAuditor.Audit(observedCommand);
            foreach (var finding in result.Findings)
            {
                _auditFindings[finding.Id] = finding;
                _persistedFindingStates[finding.Id] = new PersistedFindingState(
                    finding.Id,
                    finding.Status,
                    null);
            }
            _currentRegion = command.RegionId;
            _previewSha256 = command.Evidence.PreviewSha256;
            _structureSha256 = currentStructure.StructureSnapshotSha256;
            _qualityState = result.CanPass
                ? RegionQualityState.Passed
                : RegionQualityState.Failed;
            _lastPassedRevision = result.CanPass ? _pageRevision : null;
            RegisterAuditHistory(command);
            PersistAuditHistory();
            PersistPageQualityState();
            return result;
        });

    private StructureHashResult ComputeStructureHashCore()
    {
        var inspection = CreateInspector().Inspect(5000);
        if (inspection.Truncated)
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.CapabilityNotAvailable,
                "当前页面超过 5000 个可审计图元，不能生成完整结构哈希。");
        }
        var canonical = inspection.Shapes
            .OrderBy(item => item.SemanticId, StringComparer.Ordinal)
            .ThenBy(item => item.ShapeId)
            .Select(item => new
            {
                semantic_id = item.SemanticId,
                shape_id = item.ShapeId,
                name_u = item.NameU,
                type = item.Type,
                label = item.Label,
                bounds = item.Bounds,
                rotation_deg = item.RotationDeg,
                master_name_u = item.MasterNameU,
                layers = item.Layers.Order(StringComparer.Ordinal).ToArray(),
                connected_to = item.ConnectedTo.Order(StringComparer.Ordinal).ToArray(),
                z_order = item.ZOrder,
                style_cells = SortDictionary(item.StyleCells),
                geometry = item.Geometry,
                group = item.Group is null
                    ? null
                    : new
                    {
                        parent_semantic_id = item.Group.ParentSemanticId,
                        child_semantic_ids = item.Group.ChildSemanticIds
                            .Order(StringComparer.Ordinal)
                            .ToArray(),
                    },
                protection = SortDictionary(item.Protection),
                connection_points = item.ConnectionPoints?
                    .Order(StringComparer.Ordinal)
                    .ToArray(),
                source_metadata = SortDictionary(item.SourceMetadata),
            })
            .ToArray();
        var bytes = JsonSerializer.SerializeToUtf8Bytes(
            canonical,
            ProtocolJson.Options);
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        return new StructureHashResult(_pageRevision, hash, canonical.Length);
    }

    private bool InvalidateExternalPassIfNeeded()
    {
        if (_qualityState != RegionQualityState.Passed
            || _lastPassedRevision != _pageRevision
            || _structureSha256 == new string('0', 64))
        {
            return false;
        }
        var current = ComputeStructureHashCore();
        if (string.Equals(
                current.StructureSnapshotSha256,
                _structureSha256,
                StringComparison.Ordinal))
        {
            return false;
        }

        _pageRevision++;
        _qualityState = RegionQualityState.AwaitingAudit;
        _lastPassedRevision = null;
        _previewSha256 = new string('0', 64);
        _structureSha256 = current.StructureSnapshotSha256;
        var historyChanged = MarkAuditFindingsStale();
        if (historyChanged)
            PersistAuditHistory();
        PersistPageQualityState();
        return true;
    }

    private bool MarkAuditFindingsStale()
    {
        var changed = false;
        foreach (var id in _auditFindings.Keys.ToArray())
        {
            if (_auditFindings[id].Status == AuditFindingStatus.Stale)
                continue;
            _auditFindings[id] = _auditFindings[id] with
            {
                Status = AuditFindingStatus.Stale,
            };
            changed = true;
        }
        foreach (var id in _persistedFindingStates.Keys.ToArray())
        {
            if (_persistedFindingStates[id].Status == AuditFindingStatus.Stale)
                continue;
            _persistedFindingStates[id] = new PersistedFindingState(
                id,
                AuditFindingStatus.Stale,
                null);
            changed = true;
        }
        return changed;
    }

    private static IReadOnlyDictionary<string, TValue>? SortDictionary<TValue>(
        IReadOnlyDictionary<string, TValue>? source)
    {
        if (source is null)
            return null;
        var sorted = new SortedDictionary<string, TValue>(StringComparer.Ordinal);
        foreach (var item in source)
            sorted[item.Key] = item.Value;
        return sorted;
    }

    private FigureAuditInput ObserveAuditInput(FigureAuditInput command)
    {
        var requestedObjects = command.Objects ?? [];
        var duplicateId = requestedObjects
            .GroupBy(item => item.SemanticId, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1)?.Key;
        if (duplicateId is not null)
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.InvalidArgument,
                $"审计对象 semantic_id 重复：{duplicateId}");
        }

        var requestedById = requestedObjects.ToDictionary(
            item => item.SemanticId,
            StringComparer.Ordinal);
        var observedIds = new HashSet<string>(StringComparer.Ordinal);
        var observed = new List<FigureAuditObject>();
        foreach (var snapshot in CreateInspector().Inspect(5000).Shapes)
        {
            var semanticId = string.IsNullOrWhiteSpace(snapshot.SemanticId)
                ? $"visio-shape-{snapshot.ShapeId}"
                : snapshot.SemanticId;
            observedIds.Add(semanticId);
            requestedById.TryGetValue(semanticId, out var requested);
            var isConnector = snapshot.Type == "connector";
            var glueObserved = isConnector
                ? snapshot.ConnectedTo.Distinct(StringComparer.Ordinal).Count() >= 2
                : (bool?)null;
            var metadataValid = !string.IsNullOrWhiteSpace(snapshot.SemanticId);
            bool? rasterDeclarationValid = null;
            if (snapshot.SourceMetadata is { } metadata
                && metadata.ContainsKey("raster_declaration_field_count"))
            {
                rasterDeclarationValid =
                    metadata.GetValueOrDefault("raster_declaration_consistent") == "true"
                    && metadata.GetValueOrDefault("raster_VsiRasterTightlyCropped") == "true"
                    && metadata.GetValueOrDefault("raster_VsiRasterAtomicUnit") == "true"
                    && metadata.GetValueOrDefault("raster_VsiRasterContainsReconstructable") == "false"
                    && !string.IsNullOrWhiteSpace(
                        metadata.GetValueOrDefault("raster_VsiRasterReason"))
                    && !string.IsNullOrWhiteSpace(
                        metadata.GetValueOrDefault("raster_VsiRasterDecompositionNote"));
                metadataValid &= rasterDeclarationValid.Value;
            }

            var bounds = snapshot.Bounds;
            var insideCanvas = bounds.X >= 0
                && bounds.Y >= 0
                && bounds.X + bounds.Width <= _canvas!.WidthPx
                && bounds.Y + bounds.Height <= _canvas.HeightPx;
            observed.Add(new FigureAuditObject(
                semanticId,
                requested?.ExpectedText,
                snapshot.Label,
                insideCanvas,
                requested?.TextOverflows ?? false,
                metadataValid,
                isConnector,
                glueObserved,
                glueObserved,
                requested?.UnexpectedOverlapWith ?? [],
                requested?.AlignmentError ?? 0,
                requested?.ConnectorClearance,
                rasterDeclarationValid));
        }

        foreach (var requested in requestedObjects.Where(item =>
                     !observedIds.Contains(item.SemanticId)))
        {
            observed.Add(requested with
            {
                ActualText = null,
                IsInsideCanvas = false,
                MetadataValid = false,
                SourceGlued = requested.IsConnector ? false : null,
                TargetGlued = requested.IsConnector ? false : null,
            });
        }

        return command with
        {
            Objects = observed
                .OrderBy(item => item.SemanticId, StringComparer.Ordinal)
                .ToArray(),
        };
    }

    public AuditFindingListResult GetAuditFindings(GetAuditFindingsCommand command)
        => InvokeReady(() =>
        {
            ArgumentNullException.ThrowIfNull(command);
            var findings = _auditFindings.Values
                .Where(item => command.RegionId is null
                    || string.Equals(item.RegionId, command.RegionId, StringComparison.Ordinal))
                .Where(item => command.Status is null || item.Status == command.Status)
                .OrderBy(item => item.Id, StringComparer.Ordinal)
                .ToArray();
            return new AuditFindingListResult(_pageRevision, findings);
        });

    public AuditFinding GetAuditFinding(GetAuditFindingCommand command)
        => InvokeReady(() =>
        {
            if (command is null || string.IsNullOrWhiteSpace(command.Id))
                throw new BridgeCommandException(
                    BridgeErrorCodes.InvalidArgument,
                    "finding id 不能为空。");
            return _auditFindings.TryGetValue(command.Id, out var finding)
                ? finding
                : throw new BridgeCommandException(
                    BridgeErrorCodes.ShapeNotFound,
                    $"不存在 finding：{command.Id}");
        });

    public AuditFinding SetAuditDecision(SetAuditDecisionCommand command)
        => InvokeReady(() =>
        {
            ArgumentNullException.ThrowIfNull(command);
            if (command.ExpectedRevision != _pageRevision)
                throw new BridgeCommandException(
                    BridgeErrorCodes.RevisionConflict,
                    "审计决定使用了旧页面修订号。");
            if (!_auditFindings.TryGetValue(command.Id, out var finding))
                throw new BridgeCommandException(
                    BridgeErrorCodes.ShapeNotFound,
                    $"不存在 finding：{command.Id}");

            var status = command.Decision switch
            {
                "waive" when finding.Severity != AuditSeverity.Hard
                    && !string.IsNullOrWhiteSpace(command.Reason) => AuditFindingStatus.Waived,
                "reopen" => AuditFindingStatus.Open,
                "waive" => throw new BridgeCommandException(
                    BridgeErrorCodes.InvalidArgument,
                    "hard finding 不可豁免；warning/info 豁免必须提供原因。"),
                _ => throw new BridgeCommandException(
                    BridgeErrorCodes.InvalidArgument,
                    "decision 仅允许 waive 或 reopen。"),
            };
            var updatedEvidence = new SortedDictionary<string, string>(
                StringComparer.Ordinal);
            foreach (var item in finding.Evidence)
                updatedEvidence[item.Key] = item.Value;
            if (status == AuditFindingStatus.Waived)
                updatedEvidence["waiver_reason"] = command.Reason!;
            var updated = finding with { Status = status, Evidence = updatedEvidence };
            _auditFindings[updated.Id] = updated;
            _persistedFindingStates[updated.Id] = new PersistedFindingState(
                updated.Id,
                status,
                status == AuditFindingStatus.Waived ? command.Reason : null);
            var hasBlockingFinding = _auditFindings.Values.Any(item =>
                item.RegionId == _currentRegion
                && item.ObservedRevision == _pageRevision
                && item.Status == AuditFindingStatus.Open
                && item.Severity is AuditSeverity.Hard or AuditSeverity.Warning);
            _qualityState = hasBlockingFinding
                ? RegionQualityState.Failed
                : RegionQualityState.Passed;
            _lastPassedRevision = hasBlockingFinding ? null : _pageRevision;
            PersistAuditHistory();
            PersistPageQualityState();
            return updated;
        });

    public QualityGateResult AssertQualityGate(QualityGateCommand command)
        => InvokeReady(() =>
        {
            InvalidateExternalPassIfNeeded();
            if (command is null || string.IsNullOrWhiteSpace(command.RegionId))
                throw new BridgeCommandException(
                    BridgeErrorCodes.InvalidArgument,
                    "region_id 不能为空。");
            var openHardIds = _auditFindings.Values
                .Where(item => item.RegionId == command.RegionId
                    && item.Severity == AuditSeverity.Hard
                    && item.Status == AuditFindingStatus.Open)
                .Select(item => item.Id)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray();
            if (_qualityState != RegionQualityState.Passed
                || _lastPassedRevision != _pageRevision
                || !string.Equals(_currentRegion, command.RegionId, StringComparison.Ordinal)
                || openHardIds.Length > 0)
            {
                throw new BridgeCommandException(
                    BridgeErrorCodes.QualityGateBlocked,
                    $"区域 {command.RegionId} 尚未通过当前修订版审计。",
                    retryable: true,
                    details: new
                    {
                        region_id = command.RegionId,
                        current_region = _currentRegion,
                        page_revision = _pageRevision,
                        last_passed_revision = _lastPassedRevision,
                        quality_state = _qualityState,
                        finding_ids = openHardIds,
                    });
            }
            return new QualityGateResult(command.RegionId, _pageRevision, true);
        });

    public RegionQualitySnapshot GetQualityStatus()
        => InvokeReady(() =>
        {
            InvalidateExternalPassIfNeeded();
            return new RegionQualitySnapshot(
                _currentRegion,
                _qualityState,
                _pageRevision,
                _lastPassedRevision);
        });

    public StructureHashResult GetStructureHash()
        => InvokeReady(() =>
        {
            InvalidateExternalPassIfNeeded();
            return ComputeStructureHashCore();
        });

    public SafePatchResult ApplySafePatch(ApplySafePatchCommand command)
        => ExecuteUndoable("Codex apply safe audit patch", () =>
        {
            ArgumentNullException.ThrowIfNull(command);
            RequireRevision(command.ExpectedRevision);
            if (!_auditFindings.TryGetValue(command.FindingId, out var finding)
                || finding.Status != AuditFindingStatus.Open
                || finding.SafePatch is null)
            {
                throw new BridgeCommandException(
                    BridgeErrorCodes.ComponentContractInvalid,
                    "finding 不存在、不是 open，或没有 safe patch。");
            }
            var patch = SafePatchValidator.Validate(finding.SafePatch);
            if (patch.ExpectedRevision != _pageRevision
                || patch.ObjectIds.Except(
                    finding.ObjectIds,
                    StringComparer.Ordinal).Any())
            {
                throw new BridgeCommandException(
                    BridgeErrorCodes.RevisionConflict,
                    "safe patch 已过期或引用了 finding 之外的对象。");
            }

            var inspection = CreateInspector().Inspect(5000);
            var byId = inspection.Shapes.ToDictionary(
                item => item.SemanticId,
                StringComparer.Ordinal);
            var editor = CreateShapeEditor();
            switch (patch.Operation)
            {
                case "expand_text_box":
                    foreach (var objectId in patch.ObjectIds)
                    {
                        var shape = RequiredSnapshot(byId, objectId);
                        editor.Update(new UpdateShapeCommand(
                            objectId,
                            Width: shape.Bounds.Width * 1.1,
                            Height: shape.Bounds.Height * 1.2));
                    }
                    break;
                case "move_inside_canvas":
                    foreach (var objectId in patch.ObjectIds)
                    {
                        var shape = RequiredSnapshot(byId, objectId);
                        var x = Math.Clamp(
                            shape.Bounds.X,
                            0,
                            Math.Max(0, _canvas!.WidthPx - shape.Bounds.Width));
                        var y = Math.Clamp(
                            shape.Bounds.Y,
                            0,
                            Math.Max(0, _canvas.HeightPx - shape.Bounds.Height));
                        editor.Update(new UpdateShapeCommand(objectId, X: x, Y: y));
                    }
                    break;
                default:
                    throw new BridgeCommandException(
                        BridgeErrorCodes.CapabilityNotAvailable,
                        $"当前版本尚未实现 safe patch：{patch.Operation}");
            }

            return new SafePatchResult(
                command.FindingId,
                patch.Operation,
                patch.ObjectIds,
                _pageRevision,
                _pageRevision + 1,
                RequiresReaudit: true);
        });

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

    public AuditHistoryState ReadAuditHistoryForIntegrationTest()
        => InvokeReady(() => CreateAuditStore().Read(_solutionXmlSha256));

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

    public void EditShapeTextExternallyForIntegrationTest(
        string semanticId,
        string text)
        => InvokeReady(() =>
        {
            using var handle = GetShapeRepository().Resolve(semanticId);
            ((VisioShapeHandle)handle).Shape.Text = text;
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
            try
            {
                _masterCatalog?.Dispose();
                _masterCatalog = null;
                if (_document is not null)
                    _document.Saved = true;
                _document?.Close();
                _application?.Quit();
            }
            finally
            {
                ReleaseCom(ref _page);
                ReleaseCom(ref _document);
                ReleaseCom(ref _application);
                OwnedProcessId = null;
                _state = SessionState.Stopped;
            }
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
        _pageRevision = ReadPersistedPageRevision();
        _currentRegion = ValidatePersistedQualityContext();
        LoadOrInitializeAuditHistory();
        _qualityState = RegionQualityState.AwaitingAudit;
        _lastPassedRevision = null;
        _previewSha256 = new string('0', 64);
        _structureSha256 = ComputeStructureHashCore().StructureSnapshotSha256;
        PersistPageQualityState();
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

    private long ReadPersistedPageRevision()
    {
        var pageSheet = _page!.PageSheet;
        try
        {
            const string cellName = "User.VsiPageRevision";
            if (pageSheet.CellExistsU[
                    cellName,
                    (short)Visio.VisExistsFlags.visExistsAnywhere] == 0)
            {
                return 0;
            }
            var cell = pageSheet.CellsU[cellName];
            try
            {
                var text = cell.ResultStrU[(short)Visio.VisUnitCodes.visNoCast];
                return long.TryParse(
                    text,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var value) && value >= 0
                    ? value
                    : throw new BridgeCommandException(
                        BridgeErrorCodes.AuditStateCorrupt,
                        "PageSheet 的页面修订号无效。");
            }
            finally
            {
                Marshal.FinalReleaseComObject(cell);
            }
        }
        finally
        {
            Marshal.FinalReleaseComObject(pageSheet);
        }
    }

    private string? ReadPersistedUserCell(string cellName)
    {
        var pageSheet = _page!.PageSheet;
        try
        {
            if (pageSheet.CellExistsU[
                    cellName,
                    (short)Visio.VisExistsFlags.visExistsAnywhere] == 0)
            {
                return null;
            }
            var cell = pageSheet.CellsU[cellName];
            try
            {
                return cell.ResultStrU[(short)Visio.VisUnitCodes.visNoCast];
            }
            finally
            {
                Marshal.FinalReleaseComObject(cell);
            }
        }
        finally
        {
            Marshal.FinalReleaseComObject(pageSheet);
        }
    }

    private string ValidatePersistedQualityContext()
    {
        var protocol = ReadPersistedUserCell("User.VsiProtocolVersion");
        if (protocol is not null
            && !string.Equals(
                protocol,
                ShapeMetadataService.ProtocolVersion,
                StringComparison.Ordinal))
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.AuditStateCorrupt,
                "PageSheet 的 Test Edition 协议版本与当前 Bridge 不一致。");
        }

        var qualityState = ReadPersistedUserCell("User.VsiQualityState");
        if (qualityState is not null
            && qualityState is not (
                "planned" or
                "drawing" or
                "awaiting_audit" or
                "failed" or
                "correcting" or
                "passed"))
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.AuditStateCorrupt,
                "PageSheet 的区域质量状态无效。");
        }

        ValidatePersistedSha256("User.VsiPreviewSha256");
        ValidatePersistedSha256("User.VsiStructureSha256");
        ValidatePersistedSha256("User.VsiSolutionXmlSha256");

        var lastPassedRevision = ReadPersistedUserCell("User.VsiLastPassedRevision");
        if (!string.IsNullOrEmpty(lastPassedRevision)
            && (!long.TryParse(
                    lastPassedRevision,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var value)
                || value < 0
                || value > _pageRevision))
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.AuditStateCorrupt,
                "PageSheet 的最后通过修订号无效。");
        }

        var region = ReadPersistedUserCell("User.VsiCurrentRegion");
        return string.IsNullOrWhiteSpace(region) ? "page" : region;
    }

    private void ValidatePersistedSha256(string cellName)
    {
        var value = ReadPersistedUserCell(cellName);
        if (value is null)
            return;
        if (value.Length != 64
            || value.Any(character =>
                character is not (>= '0' and <= '9')
                && character is not (>= 'a' and <= 'f')))
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.AuditStateCorrupt,
                $"PageSheet 的 {cellName} 不是规范的 SHA-256。");
        }
    }

    private void LoadOrInitializeAuditHistory()
    {
        var store = CreateAuditStore();
        var persistedHash = ReadPersistedUserCell("User.VsiSolutionXmlSha256");
        var hasHash = !string.IsNullOrWhiteSpace(persistedHash)
            && persistedHash != new string('0', 64);
        var currentXml = new VisioSolutionXmlBackend(_document!)
            .Read(AuditSolutionXmlStore.SolutionName);
        if (!hasHash)
        {
            if (currentXml is not null)
            {
                throw new BridgeCommandException(
                    BridgeErrorCodes.AuditStateCorrupt,
                    "文档包含未绑定哈希的 Test Edition SolutionXML。");
            }
            ApplyAuditHistory(new AuditHistoryState([], [], [], []));
            PersistAuditHistory();
            return;
        }

        ApplyAuditHistory(store.Read(persistedHash!));
        _solutionXmlSha256 = persistedHash!;
    }

    private void RegisterAuditHistory(FigureAuditInput command)
    {
        _registeredRegionIds.Add(command.RegionId);
        var entry = new PersistedAuditEntry(
            command.PageRevision,
            command.RegionId == "page" ? null : command.RegionId,
            command.Evidence.AuditProfile,
            HashAuditEvidence(command.Evidence));
        if (entry.RegionId is null)
            _wholePageAuditHistory.Add(entry);
        else
            _regionAuditHistory.Add(entry);
    }

    private void PersistAuditHistory()
    {
        var store = CreateAuditStore();
        var state = new AuditHistoryState(
            _registeredRegionIds.ToArray(),
            _regionAuditHistory.ToArray(),
            _wholePageAuditHistory.ToArray(),
            _persistedFindingStates.Values.ToArray());
        _solutionXmlSha256 = store.Write(state);
        ApplyAuditHistory(store.Read(_solutionXmlSha256));
    }

    private void ApplyAuditHistory(AuditHistoryState state)
    {
        _registeredRegionIds.Clear();
        _registeredRegionIds.UnionWith(state.RegionIds);
        _regionAuditHistory.Clear();
        _regionAuditHistory.AddRange(state.RegionAudits);
        _wholePageAuditHistory.Clear();
        _wholePageAuditHistory.AddRange(state.WholePageAudits);
        _persistedFindingStates.Clear();
        foreach (var finding in state.Findings)
            _persistedFindingStates[finding.FindingId] = finding;
    }

    private AuditSolutionXmlStore CreateAuditStore() =>
        new(new VisioSolutionXmlBackend(_document!));

    private static string HashAuditEvidence(AuditEvidence evidence)
    {
        var canonical = string.Join(
            "\n",
            evidence.PageRevision.ToString(CultureInfo.InvariantCulture),
            evidence.RegionId,
            evidence.PreviewSha256,
            evidence.ReferenceCropSha256,
            evidence.StructureSnapshotSha256,
            evidence.AuditProfile);
        return Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))
            .ToLowerInvariant();
    }

    private void PersistPageQualityState()
    {
        var pageSheet = _page!.PageSheet;
        try
        {
            var store = new VisioRasterMetadataStore(pageSheet);
            store.WriteUserCell("User.VsiProtocolVersion", ShapeMetadataService.ProtocolVersion);
            store.WriteUserCell(
                "User.VsiPageRevision",
                _pageRevision.ToString(CultureInfo.InvariantCulture));
            store.WriteUserCell("User.VsiCurrentRegion", _currentRegion);
            store.WriteUserCell(
                "User.VsiQualityState",
                FormatQualityState(_qualityState));
            store.WriteUserCell(
                "User.VsiLastPassedRevision",
                _lastPassedRevision?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
            store.WriteUserCell("User.VsiPreviewSha256", _previewSha256);
            store.WriteUserCell("User.VsiStructureSha256", _structureSha256);
            store.WriteUserCell("User.VsiSolutionXmlSha256", _solutionXmlSha256);
        }
        finally
        {
            Marshal.FinalReleaseComObject(pageSheet);
        }
    }

    private static string FormatQualityState(RegionQualityState state) => state switch
    {
        RegionQualityState.Planned => "planned",
        RegionQualityState.Drawing => "drawing",
        RegionQualityState.AwaitingAudit => "awaiting_audit",
        RegionQualityState.Failed => "failed",
        RegionQualityState.Correcting => "correcting",
        RegionQualityState.Passed => "passed",
        _ => throw new BridgeCommandException(
            BridgeErrorCodes.AuditStateCorrupt,
            "未知的区域质量状态。"),
    };

    private CapabilityProbeSnapshot CreateCapabilitySnapshot()
    {
        var application = RequireReadyApplication();
        var installed = _capabilityService.ProbeInstalled(_pluginRoot);
        var session = _capabilityService.ProbeSession(
            installed,
            application.Version,
            application.Build.ToString(CultureInfo.InvariantCulture),
            ReadOpenMasterCapabilities(application));
        return _capabilityService.ProbeDocument(
            session,
            _page!.Name,
            _canvas!.WidthPx,
            _canvas.HeightPx,
            ReadCurrentLayerNames(),
            CountCurrentContainers());
    }

    private static IReadOnlyList<CapabilityEntry> ReadOpenMasterCapabilities(
        Visio.InvisibleApp application)
    {
        var result = new List<CapabilityEntry>();
        var documents = application.Documents;
        try
        {
            for (var documentIndex = 1; documentIndex <= documents.Count; documentIndex++)
            {
                Visio.Document? document = documents[documentIndex];
                try
                {
                    if (document.Type != Visio.VisDocumentTypes.visTypeStencil)
                    {
                        continue;
                    }
                    var stencilName = document.Name;
                    var masters = document.Masters;
                    try
                    {
                        for (var masterIndex = 1; masterIndex <= masters.Count; masterIndex++)
                        {
                            Visio.Master? master = masters[masterIndex];
                            try
                            {
                                result.Add(new CapabilityEntry(
                                    $"open-master:{stencilName}:{master.NameU}",
                                    master.NameU,
                                    true,
                                    "session",
                                    Metadata: new Dictionary<string, string>(StringComparer.Ordinal)
                                    {
                                        ["stencil"] = stencilName,
                                    }));
                            }
                            finally
                            {
                                ReleaseCom(ref master);
                            }
                        }
                    }
                    finally
                    {
                        ReleaseCom(ref masters);
                    }
                }
                finally
                {
                    ReleaseCom(ref document);
                }
            }
        }
        finally
        {
            ReleaseCom(ref documents);
        }
        return result;
    }

    private IReadOnlyList<string> ReadCurrentLayerNames()
    {
        var result = new List<string>();
        var layers = _page!.Layers;
        try
        {
            for (var index = 1; index <= layers.Count; index++)
            {
                Visio.Layer? layer = layers[index];
                try
                {
                    result.Add(layer.Name);
                }
                finally
                {
                    ReleaseCom(ref layer);
                }
            }
        }
        finally
        {
            ReleaseCom(ref layers);
        }
        return result;
    }

    private int CountCurrentContainers()
    {
        var count = 0;
        var shapes = _page!.Shapes;
        try
        {
            for (var index = 1; index <= shapes.Count; index++)
            {
                Visio.Shape? shape = shapes[index];
                Visio.Master? master = null;
                try
                {
                    master = shape.Master;
                    if (master is not null
                        && string.Equals(
                            master.NameU,
                            "Codex Container",
                            StringComparison.Ordinal))
                    {
                        count++;
                    }
                }
                finally
                {
                    ReleaseCom(ref master);
                    ReleaseCom(ref shape);
                }
            }
        }
        finally
        {
            ReleaseCom(ref shapes);
        }
        return count;
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
                    "未保存文档必须先通过 visio_test_live_save 指定绝对 .vsdx 路径。");
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

    private void RequireRevision(long expectedRevision)
    {
        if (expectedRevision != _pageRevision)
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.RevisionConflict,
                $"页面修订号为 {_pageRevision}，合同期望 {expectedRevision}。",
                details: new
                {
                    current_revision = _pageRevision,
                    expected_revision = expectedRevision,
                });
        }
    }

    private static ShapeSnapshot RequiredSnapshot(
        IReadOnlyDictionary<string, ShapeSnapshot> shapes,
        string semanticId) =>
        shapes.TryGetValue(semanticId, out var shape)
            ? shape
            : throw new BridgeCommandException(
                BridgeErrorCodes.ShapeNotFound,
                $"不存在 safe patch 对象：{semanticId}");

    private ComponentUpdateResult ComponentUpdate(
        string semanticId,
        IReadOnlyList<string> affectedIds) =>
        new(semanticId, affectedIds, _pageRevision, _pageRevision + 1);

    private CompositeComponentMutationResult ReplaceComposite(
        CompositeComponentContract contract,
        CompositeComponentPlan plan)
    {
        RequireRevision(contract.ExpectedRevision);
        CreateShapeEditor().Delete(new DeleteShapeCommand(contract.SemanticId, "delete"));
        GetShapeRepository().RebuildIndex();
        return ExecuteComposite(contract, plan);
    }

    private static double[] ResolveComponentDimensions(
        IReadOnlyList<double>? supplied,
        int count,
        double total,
        string field)
    {
        if (supplied is null)
            return Enumerable.Repeat(total / count, count).ToArray();
        if (supplied.Count != count
            || supplied.Any(value => !double.IsFinite(value) || value <= 0)
            || Math.Abs(supplied.Sum() - total) > 0.001)
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.ComponentContractInvalid,
                $"{field} 的数量、数值或总和无效。");
        }
        return supplied.ToArray();
    }

    private CompositeComponentMutationResult ExecuteComposite(
        CompositeComponentContract contract,
        CompositeComponentPlan plan)
    {
        plan.ValidateFor(contract);
        if (_pageRevision != contract.ExpectedRevision)
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.RevisionConflict,
                $"页面修订号为 {_pageRevision}，合同期望 {contract.ExpectedRevision}。",
                details: new
                {
                    current_revision = _pageRevision,
                    expected_revision = contract.ExpectedRevision,
                });
        }

        var shapeFactory = CreateShapeFactory();
        var pathFactory = CreateFreeformBuilder();
        foreach (var primitive in plan.Primitives)
        {
            var parameters = primitive.Parameters;
            var shape = ReadString(parameters, "shape")
                ?? (primitive.Kind == "path" ? "rectangle" : "rectangle");
            var label = ReadString(parameters, "text");
            var fill = ReadString(parameters, "fill")
                ?? (primitive.Kind == "path" ? "#222222" : "#FFFFFF");
            var stroke = ReadString(parameters, "stroke")
                ?? ReadString(parameters, "border_color")
                ?? (primitive.Kind == "path" ? fill : "#222222");
            var strokeWidth = ReadDouble(parameters, "stroke_width")
                ?? (primitive.Kind == "path" ? 0d : 0.75d);
            var fontSize = ReadDouble(parameters, "font_size") ?? 10d;
            var rotation = ReadDouble(parameters, "rotation") ?? 0d;
            var style = new ShapeStyle(
                FillColor: fill,
                StrokeColor: stroke,
                StrokeWidthPt: strokeWidth,
                FontColor: ReadString(parameters, "text_color") ?? "#111111",
                FontSizePt: fontSize,
                RotationDeg: rotation);
            if (primitive.Kind == "path")
            {
                var startY = ReadDouble(parameters, "start_y_percent");
                var endY = ReadDouble(parameters, "end_y_percent");
                IReadOnlyList<ScenePathCommand> commands = startY is not null
                    && endY is not null
                    ? [
                        new ScenePathCommand("M", 0, startY.Value),
                        new ScenePathCommand("L", 100, endY.Value),
                    ]
                    : primitive.Bounds.Width >= primitive.Bounds.Height
                        ? [
                            new ScenePathCommand("M", 0, 50),
                            new ScenePathCommand("L", 100, 50),
                        ]
                        : [
                            new ScenePathCommand("M", 50, 0),
                            new ScenePathCommand("L", 50, 100),
                        ];
                pathFactory.Add(new AddPathCommand(
                    primitive.SemanticId,
                    primitive.Bounds,
                    [new PathSection(commands)],
                    style));
            }
            else
            {
                shapeFactory.Add(new AddShapeCommand(
                    primitive.SemanticId,
                    shape,
                    primitive.Bounds,
                    label,
                    style));
            }
        }

        var metadata = new ComponentMetadataService();
        foreach (var primitive in plan.Primitives)
            WriteComponentMetadata(
                primitive.SemanticId,
                metadata.BuildChildMetadata(contract, primitive));

        var childIds = plan.Primitives.Select(item => item.SemanticId).ToArray();
        CreateGroupingService().Group(new GroupShapesCommand(contract.SemanticId, childIds));
        foreach (var point in plan.ConnectionPoints)
        {
            using var handle = GetShapeRepository().Resolve(contract.SemanticId);
            ConnectionPointService.AddNamedPoint(
                ((VisioShapeHandle)handle).Shape,
                point.Name,
                point.Position.X,
                point.Position.Y,
                0,
                0);
        }
        WriteComponentMetadata(
            contract.SemanticId,
            metadata.BuildParentMetadata(contract, plan));

        var previousRevision = _pageRevision;
        return new CompositeComponentMutationResult(
            contract.SemanticId,
            contract.ComponentType,
            childIds,
            previousRevision,
            previousRevision + 1);
    }

    private void WriteComponentMetadata(
        string semanticId,
        IReadOnlyDictionary<string, string> metadata)
    {
        using var handle = GetShapeRepository().Resolve(semanticId);
        var store = new VisioRasterMetadataStore(((VisioShapeHandle)handle).Shape);
        foreach (var item in metadata)
            store.WriteUserCell(item.Key, item.Value);
    }

    private static string? ReadString(System.Text.Json.JsonElement parameters, string name) =>
        parameters.TryGetProperty(name, out var value)
        && value.ValueKind == System.Text.Json.JsonValueKind.String
            ? value.GetString()
            : null;

    private static double? ReadDouble(System.Text.Json.JsonElement parameters, string name) =>
        parameters.TryGetProperty(name, out var value)
        && value.ValueKind == System.Text.Json.JsonValueKind.Number
        && value.TryGetDouble(out var result)
            ? result
            : null;

    private T ExecuteUndoable<T>(string name, Func<T> operation)
    {
        try
        {
            return InvokeReady(() =>
            {
                if (InvalidateExternalPassIfNeeded())
                {
                    throw new BridgeCommandException(
                        BridgeErrorCodes.StaleAuditEvidence,
                        "检测到 Visio 页面存在未经过 Bridge 的外部修改；旧审查证据已失效，请重新检查当前修订版。",
                        retryable: true,
                        details: new { current_revision = _pageRevision });
                }
                using var scope = new UndoScope(RequireReadyApplication(), name);
                var result = operation();
                _pageRevision++;
                _qualityState = RegionQualityState.AwaitingAudit;
                _lastPassedRevision = null;
                _previewSha256 = new string('0', 64);
                _structureSha256 = new string('0', 64);
                if (MarkAuditFindingsStale())
                    PersistAuditHistory();
                PersistPageQualityState();
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
