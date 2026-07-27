using System.Text.Json;
using VisioTestBridge.Contracts;
using VisioTestBridge.Core.Abstractions;

namespace VisioTestBridge.Core.Commands;

public sealed class CommandRouter
{
    private readonly IVisioSession _session;

    public CommandRouter(IVisioSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public object Route(string command, JsonElement parameters) => command switch
    {
        "launch_new" => LaunchNew(Deserialize<LaunchNewCommand>(parameters)),
        "open_document" => OpenDocument(Deserialize<OpenDocumentCommand>(parameters)),
        "status" => _session.GetStatus(),
        "get_capabilities" => _session.GetCapabilities(),
        "get_capability_details" => _session.GetCapabilityDetails(
            Deserialize<GetCapabilityDetailsCommand>(parameters)),
        "add_shape" => _session.AddShape(Deserialize<AddShapeCommand>(parameters)),
        "add_image" => _session.AddImage(Deserialize<AddImageCommand>(parameters)),
        "add_path" => _session.AddPath(Deserialize<AddPathCommand>(parameters)),
        "update_path" => _session.UpdatePath(Deserialize<UpdatePathCommand>(parameters)),
        "add_connector" => _session.AddConnector(Deserialize<AddConnectorCommand>(parameters)),
        "add_table" => _session.AddTable(Deserialize<TableComponentContract>(parameters)),
        "add_chart" => _session.AddChart(Deserialize<ChartComponentContract>(parameters)),
        "add_matrix" => _session.AddMatrix(Deserialize<MatrixComponentContract>(parameters)),
        "add_grid" => _session.AddGrid(Deserialize<GridComponentContract>(parameters)),
        "update_table_cell" => _session.UpdateTableCell(
            Deserialize<UpdateTableCellCommand>(parameters)),
        "update_table_layout" => _session.UpdateTableLayout(
            Deserialize<UpdateTableLayoutCommand>(parameters)),
        "update_chart" => _session.UpdateChart(Deserialize<ChartComponentContract>(parameters)),
        "update_matrix_cell" => _session.UpdateMatrixCell(
            Deserialize<UpdateMatrixCellCommand>(parameters)),
        "update_grid" => _session.UpdateGrid(Deserialize<GridComponentContract>(parameters)),
        "audit_figure" => _session.AuditFigure(Deserialize<FigureAuditInput>(parameters)),
        "get_audit_findings" => _session.GetAuditFindings(
            Deserialize<GetAuditFindingsCommand>(parameters)),
        "get_audit_finding" => _session.GetAuditFinding(
            Deserialize<GetAuditFindingCommand>(parameters)),
        "set_audit_decision" => _session.SetAuditDecision(
            Deserialize<SetAuditDecisionCommand>(parameters)),
        "assert_quality_gate" => _session.AssertQualityGate(
            Deserialize<QualityGateCommand>(parameters)),
        "quality_status" => _session.GetQualityStatus(),
        "get_structure_hash" => _session.GetStructureHash(),
        "apply_safe_patch" => _session.ApplySafePatch(
            Deserialize<ApplySafePatchCommand>(parameters)),
        "inspect" => _session.Inspect(Deserialize<InspectCommand>(parameters).MaxShapes),
        "update_shape" => _session.UpdateShape(Deserialize<UpdateShapeCommand>(parameters)),
        "delete_shape" => _session.DeleteShape(Deserialize<DeleteShapeCommand>(parameters)),
        "clear" => _session.Clear(Deserialize<ClearCommand>(parameters).Confirm),
        "fit" => _session.Fit(Deserialize<FitCommand>(parameters).Mode),
        "preview" => _session.ExportPreview(Deserialize<PreviewCommand>(parameters).WidthPx),
        "save" => _session.Save(Deserialize<SaveCommand>(parameters)),
        "group_shapes" => _session.GroupShapes(Deserialize<GroupShapesCommand>(parameters)),
        "ungroup_shape" => _session.UngroupShape(Deserialize<UngroupShapeCommand>(parameters)),
        "add_to_container" => _session.AddToContainer(Deserialize<AddToContainerCommand>(parameters)),
        "set_z_order" => _session.SetZOrder(Deserialize<SetZOrderCommand>(parameters)),
        "assign_layer" => _session.AssignLayer(Deserialize<AssignLayerCommand>(parameters)),
        "align_shapes" => _session.AlignShapes(Deserialize<AlignShapesCommand>(parameters)),
        "distribute_shapes" => _session.DistributeShapes(Deserialize<DistributeShapesCommand>(parameters)),
        "match_size" => _session.MatchSize(Deserialize<MatchSizeCommand>(parameters)),
        "set_protection" => _session.SetProtection(Deserialize<SetProtectionCommand>(parameters)),
        "duplicate_shape" => _session.DuplicateShape(Deserialize<DuplicateShapeCommand>(parameters)),
        "add_scientific_component" => _session.AddScientificComponent(Deserialize<AddScientificComponentCommand>(parameters)),
        "get_cells" => _session.GetCells(Deserialize<GetCellsCommand>(parameters)),
        "set_cells" => _session.SetCells(Deserialize<SetCellsCommand>(parameters)),
        "add_connection_point" => _session.AddConnectionPoint(Deserialize<AddConnectionPointCommand>(parameters)),
        "get_geometry" => _session.GetGeometry(Deserialize<GetGeometryCommand>(parameters)),
        "prepare_scene" => _session.PrepareScene(Deserialize<DrawSceneCommand>(parameters)),
        "ensure_layers" => _session.EnsureLayers(Deserialize<EnsureLayersCommand>(parameters)),
        "refresh" => _session.Refresh(),
        "close_document" => CloseDocument(Deserialize<CloseDocumentCommand>(parameters)),
        "shutdown" => Shutdown(Deserialize<ShutdownCommand>(parameters)),
        _ => throw BridgeCommandException.Unsupported($"不支持命令: {command}"),
    };

    private object LaunchNew(LaunchNewCommand command) => _session.LaunchNew(
        command.PageName,
        command.WidthPx,
        command.HeightPx,
        command.Dpi,
        command.Maximize);

    private object OpenDocument(OpenDocumentCommand command) => _session.Open(
        command.FilePath,
        command.PageName,
        command.Dpi,
        command.Maximize);

    private object Shutdown(ShutdownCommand command)
    {
        _session.Shutdown(command.Confirm, command.DirtyAction);
        return new { shutdown = _session.State == Session.SessionState.Stopped };
    }

    private object CloseDocument(CloseDocumentCommand command)
    {
        _session.CloseDocument(command.Action);
        return new { closed = _session.State == Session.SessionState.Stopped };
    }

    private static T Deserialize<T>(JsonElement parameters) =>
        parameters.Deserialize<T>(ProtocolJson.Options)
        ?? throw new BridgeCommandException(
            BridgeErrorCodes.InvalidArgument,
            "命令参数不能为空。");
}
