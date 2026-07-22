using System.Text.Json;
using VisioBridge.Contracts;
using VisioBridge.Core.Files;

namespace VisioBridge.Core.Validation;

public sealed class FidelityValidator
{
    public FidelityValidationResult Validate(
        ScientificScene scene,
        PageInspectionResult inspection,
        double minimumPrimitiveRatio = 0.9)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(inspection);
        SceneGraphValidator.Validate(scene);
        if (!double.IsFinite(minimumPrimitiveRatio) || minimumPrimitiveRatio is <= 0 or > 1)
            throw new BridgeCommandException(BridgeErrorCodes.InvalidArgument, "minimum_primitive_ratio 必须位于 0–1。" );

        var errors = new List<string>();
        var warnings = new List<string>();
        var snapshots = inspection.Shapes
            .Where(shape => !string.IsNullOrWhiteSpace(shape.SemanticId))
            .GroupBy(shape => shape.SemanticId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        var expectedIds = scene.Nodes.Select(node => node.SemanticId)
            .Concat(scene.Connectors.Select(connector => connector.SemanticId)).ToArray();
        foreach (var id in expectedIds)
        {
            if (!snapshots.TryGetValue(id, out var matches)) errors.Add($"缺少 semantic_id：{id}");
            else if (matches.Length != 1) errors.Add($"semantic_id 不唯一：{id}");
        }

        var textMismatches = 0;
        foreach (var node in scene.Nodes)
        {
            var expectedText = node switch
            {
                SceneTextNode text => text.RichText?.Text ?? text.Text,
                SceneShapeNode shape => shape.RichText?.Text ?? shape.Label,
                _ => null,
            };
            if (expectedText is null || !snapshots.TryGetValue(node.SemanticId, out var matches) || matches.Length != 1) continue;
            if (!string.Equals(matches[0].Label, expectedText, StringComparison.Ordinal))
            {
                textMismatches++;
                errors.Add($"文字不匹配：{node.SemanticId}");
            }
        }

        var glued = 0;
        foreach (var connector in scene.Connectors)
        {
            if (!snapshots.TryGetValue(connector.SemanticId, out var matches) || matches.Length != 1) continue;
            if (matches[0].Type != "connector" || matches[0].ConnectedTo.Count != 2)
                errors.Add($"连接线未双端粘附：{connector.SemanticId}");
            else glued++;
        }

        var expectedPrimitives = scene.Nodes.Count + scene.Connectors.Count;
        var actualPrimitives = inspection.ShapeCount + inspection.ConnectorCount;
        if (actualPrimitives < Math.Ceiling(expectedPrimitives * minimumPrimitiveRatio))
            errors.Add($"图元预算不足：实际 {actualPrimitives}，期望至少 {Math.Ceiling(expectedPrimitives * minimumPrimitiveRatio)}。" );
        if (scene.Regions.Any(region => region.Checkpoint) && inspection.Truncated)
            warnings.Add("Inspect 结果被截断，区域完整性无法全部确认。" );
        var imageNodes = scene.Nodes.OfType<SceneImageNode>().Count();
        if (imageNodes > 0) warnings.Add($"场景包含 {imageNodes} 个局部图片 Shape；其内部像素不可单独编辑。" );

        var metrics = new FidelityStructureMetrics(
            expectedIds.Length,
            expectedIds.Count(id => snapshots.ContainsKey(id)),
            expectedPrimitives,
            actualPrimitives,
            scene.Connectors.Count,
            glued,
            textMismatches);
        return new FidelityValidationResult(errors.Count == 0, metrics, errors, warnings);
    }

    public FidelityValidationResult ValidateFiles(ValidateFidelityCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!Path.IsPathFullyQualified(command.ScenePath) || !File.Exists(command.ScenePath))
            throw new BridgeCommandException(BridgeErrorCodes.DocumentNotFound, "scene_path 必须是已存在的绝对 JSON 路径。" );
        ScientificScene scene;
        try
        {
            scene = JsonSerializer.Deserialize<ScientificScene>(File.ReadAllText(command.ScenePath), ProtocolJson.Options)
                ?? throw new JsonException("scene 为空");
        }
        catch (JsonException error)
        {
            throw new BridgeCommandException(BridgeErrorCodes.InvalidArgument, "scene_path 不是有效 Scientific SceneGraph。", details: new { error = error.Message });
        }
        using var service = new VisioFileModeService();
        var file = service.Inspect(new InspectFileCommand(command.InputPath, 5000));
        if (file.Pages.Count != 1)
            throw new BridgeCommandException(BridgeErrorCodes.InvalidArgument, "fidelity 验证当前要求 VSDX 只有一个页面。" );
        return Validate(scene, file.Pages[0], command.MinimumPrimitiveRatio);
    }
}
