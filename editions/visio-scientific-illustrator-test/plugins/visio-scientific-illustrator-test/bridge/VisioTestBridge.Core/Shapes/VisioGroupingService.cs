using System.Runtime.InteropServices;
using VisioTestBridge.Contracts;
using VisioTestBridge.Core.Interop;
using Visio = Microsoft.Office.Interop.Visio;

namespace VisioTestBridge.Core.Shapes;

internal sealed class VisioGroupingService
{
    private readonly Visio.Page _page;
    private readonly ShapeRepository _repository;

    public VisioGroupingService(Visio.Page page, ShapeRepository repository)
    {
        _page = page;
        _repository = repository;
    }

    public GroupMutationResult Group(GroupShapesCommand command)
    {
        ValidateMembers(command.MemberSemanticIds, minimum: 2);
        var handles = ResolveMembers(command.MemberSemanticIds);
        Visio.Selection? selection = null;
        Visio.Shape? group = null;
        try
        {
            selection = _page.CreateSelection(
                Visio.VisSelectionTypes.visSelTypeEmpty,
                Visio.VisSelectMode.visSelModeOnlySuper,
                Type.Missing);
            foreach (var handle in handles)
            {
                selection.Select(
                    ((VisioShapeHandle)handle).Shape,
                    (short)Visio.VisSelectArgs.visSelect);
            }
            group = selection.Group();
            using var groupHandle = new VisioShapeHandle(group);
            group = null;
            _repository.Register(groupHandle, command.GroupSemanticId);
            var result = new GroupMutationResult(
                command.GroupSemanticId,
                groupHandle.Id,
                command.MemberSemanticIds);
            return result;
        }
        catch (COMException error)
        {
            var translated = ComExceptionTranslator.Translate(error);
            if (translated.Code == BridgeErrorCodes.VisioBusy)
            {
                throw translated;
            }
            throw new BridgeCommandException(
                BridgeErrorCodes.InvalidArgument,
                "只能分组当前页面上的顶层 Shape。",
                details: new { hresult = error.HResult });
        }
        finally
        {
            if (group is not null) Marshal.ReleaseComObject(group);
            if (selection is not null) Marshal.ReleaseComObject(selection);
            foreach (var handle in handles) handle.Dispose();
        }
    }

    public UngroupMutationResult Ungroup(UngroupShapeCommand command)
    {
        using var handle = _repository.Resolve(command.GroupSemanticId);
        var shape = ((VisioShapeHandle)handle).Shape;
        if (shape.Type != (short)Visio.VisShapeTypes.visTypeGroup)
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.InvalidArgument,
                $"Shape 不是 group: {command.GroupSemanticId}");
        }
        var children = shape.Shapes;
        try
        {
            var count = children.Count;
            shape.Ungroup();
            return new UngroupMutationResult(command.GroupSemanticId, count);
        }
        finally
        {
            Marshal.ReleaseComObject(children);
        }
    }

    public ContainerMutationResult AddToContainer(AddToContainerCommand command)
    {
        ValidateMembers(command.MemberSemanticIds, minimum: 1);
        if (command.MemberSemanticIds.Contains(
                command.ContainerSemanticId,
                StringComparer.Ordinal))
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.InvalidArgument,
                "容器不能把自己作为成员。");
        }

        using var containerHandle = _repository.Resolve(command.ContainerSemanticId);
        var handles = ResolveMembers(command.MemberSemanticIds);
        Visio.ContainerProperties? properties = null;
        try
        {
            properties = ((VisioShapeHandle)containerHandle).Shape.ContainerProperties;
            if (properties is null)
            {
                throw new BridgeCommandException(
                    BridgeErrorCodes.InvalidArgument,
                    $"Shape 不是 Visio container: {command.ContainerSemanticId}");
            }
            foreach (var handle in handles)
            {
                properties.AddMember(
                    ((VisioShapeHandle)handle).Shape,
                    Visio.VisMemberAddOptions.visMemberAddExpandContainer);
            }
            var members = properties.GetMemberShapes(0);
            var memberIds = new HashSet<int>(members.Cast<int>());
            var verified = handles.Count(handle => memberIds.Contains(handle.Id));
            if (verified != handles.Count)
            {
                throw new BridgeCommandException(
                    BridgeErrorCodes.InternalError,
                    "Visio 没有确认全部 container membership。");
            }
            return new ContainerMutationResult(
                command.ContainerSemanticId,
                command.MemberSemanticIds,
                verified);
        }
        catch (COMException error)
        {
            var translated = ComExceptionTranslator.Translate(error);
            if (translated.Code == BridgeErrorCodes.VisioBusy)
            {
                throw translated;
            }
            throw new BridgeCommandException(
                BridgeErrorCodes.InvalidArgument,
                "只能把当前页面上的顶层 Shape 加入有效 Visio container。",
                details: new { hresult = error.HResult });
        }
        finally
        {
            if (properties is not null) Marshal.ReleaseComObject(properties);
            foreach (var handle in handles) handle.Dispose();
        }
    }

    private List<Abstractions.IShapeHandle> ResolveMembers(
        IReadOnlyList<string> semanticIds)
    {
        var result = new List<Abstractions.IShapeHandle>();
        try
        {
            foreach (var semanticId in semanticIds)
            {
                result.Add(_repository.Resolve(semanticId));
            }
            return result;
        }
        catch
        {
            foreach (var handle in result) handle.Dispose();
            throw;
        }
    }

    private static void ValidateMembers(IReadOnlyList<string> semanticIds, int minimum)
    {
        ArgumentNullException.ThrowIfNull(semanticIds);
        if (semanticIds.Count < minimum
            || semanticIds.Distinct(StringComparer.Ordinal).Count() != semanticIds.Count)
        {
            throw new BridgeCommandException(
                BridgeErrorCodes.InvalidArgument,
                $"member_semantic_ids 至少需要 {minimum} 个且不能重复。");
        }
    }
}
