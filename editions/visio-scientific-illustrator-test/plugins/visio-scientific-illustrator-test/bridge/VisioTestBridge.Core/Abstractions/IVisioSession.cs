using VisioTestBridge.Contracts;
using VisioTestBridge.Core.Session;

namespace VisioTestBridge.Core.Abstractions;

public interface IVisioSession : IDisposable
{
    SessionState State { get; }

    int? OwnedProcessId { get; }

    SessionStatus GetStatus();

    CapabilitySummary GetCapabilities();

    CapabilityDetailsResult GetCapabilityDetails(GetCapabilityDetailsCommand command);

    ShapeMutationResult AddShape(AddShapeCommand command);

    ShapeMutationResult AddImage(AddImageCommand command);

    PathMutationResult AddPath(AddPathCommand command);

    PathMutationResult UpdatePath(UpdatePathCommand command);

    ConnectorMutationResult AddConnector(AddConnectorCommand command);

    CompositeComponentMutationResult AddTable(TableComponentContract command);

    CompositeComponentMutationResult AddChart(ChartComponentContract command);

    CompositeComponentMutationResult AddMatrix(MatrixComponentContract command);

    CompositeComponentMutationResult AddGrid(GridComponentContract command);

    ComponentUpdateResult UpdateTableCell(UpdateTableCellCommand command);

    ComponentUpdateResult UpdateTableLayout(UpdateTableLayoutCommand command);

    CompositeComponentMutationResult UpdateChart(ChartComponentContract command);

    ComponentUpdateResult UpdateMatrixCell(UpdateMatrixCellCommand command);

    CompositeComponentMutationResult UpdateGrid(GridComponentContract command);

    FigureAuditResult AuditFigure(FigureAuditInput command);

    AuditFindingListResult GetAuditFindings(GetAuditFindingsCommand command);

    AuditFinding GetAuditFinding(GetAuditFindingCommand command);

    AuditFinding SetAuditDecision(SetAuditDecisionCommand command);

    QualityGateResult AssertQualityGate(QualityGateCommand command);

    RegionQualitySnapshot GetQualityStatus();

    StructureHashResult GetStructureHash();

    SafePatchResult ApplySafePatch(ApplySafePatchCommand command);

    PageInspectionResult Inspect(int maxShapes);

    ShapeSnapshot UpdateShape(UpdateShapeCommand command);

    DeleteResult DeleteShape(DeleteShapeCommand command);

    ClearResult Clear(bool confirm);

    PreviewResult ExportPreview(int widthPx);

    object Fit(string mode);

    SaveResult Save(SaveCommand command);

    GroupMutationResult GroupShapes(GroupShapesCommand command);

    UngroupMutationResult UngroupShape(UngroupShapeCommand command);

    ContainerMutationResult AddToContainer(AddToContainerCommand command);

    LayoutMutationResult SetZOrder(SetZOrderCommand command);

    LayoutMutationResult AssignLayer(AssignLayerCommand command);

    LayoutMutationResult AlignShapes(AlignShapesCommand command);

    LayoutMutationResult DistributeShapes(DistributeShapesCommand command);

    LayoutMutationResult MatchSize(MatchSizeCommand command);

    LayoutMutationResult SetProtection(SetProtectionCommand command);

    LayoutMutationResult DuplicateShape(DuplicateShapeCommand command);

    ScientificComponentMutationResult AddScientificComponent(AddScientificComponentCommand command);

    ShapeSheetCellsResult GetCells(GetCellsCommand command);

    ShapeSheetCellsResult SetCells(SetCellsCommand command);

    ConnectionPointMutationResult AddConnectionPoint(AddConnectionPointCommand command);

    GeometryInspectionResult GetGeometry(GetGeometryCommand command);

    SceneExecutionPlan PrepareScene(DrawSceneCommand command);

    EnsureLayersResult EnsureLayers(EnsureLayersCommand command);

    object Refresh();

    LaunchResult LaunchNew(
        string pageName,
        int widthPx,
        int heightPx,
        int dpi,
        bool maximize);

    LaunchResult Open(
        string absoluteVsdxPath,
        string? pageName,
        int dpi,
        bool maximize);

    void CloseDocument(string action);

    void Shutdown(bool confirm, string dirtyAction);
}
