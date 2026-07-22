using System.Text.Json.Serialization;

namespace VisioBridge.Contracts;

public sealed record ShapeSheetValueContract(
    string Kind,
    double? Number = null,
    string? Unit = null,
    string? Color = null,
    bool? Boolean = null,
    int? Integer = null);

public sealed record ShapeSheetWrite(
    string Cell,
    ShapeSheetValueContract Value);

public sealed record GetCellsCommand(
    [property: JsonPropertyName("semantic_id")] string SemanticId,
    IReadOnlyList<string> Cells);

public sealed record SetCellsCommand(
    [property: JsonPropertyName("semantic_id")] string SemanticId,
    IReadOnlyList<ShapeSheetWrite> Writes);

public sealed record AddConnectionPointCommand(
    [property: JsonPropertyName("semantic_id")] string SemanticId,
    string Name,
    double X,
    double Y,
    [property: JsonPropertyName("direction_x")] double DirectionX = 0,
    [property: JsonPropertyName("direction_y")] double DirectionY = 0);

public sealed record GetGeometryCommand(
    [property: JsonPropertyName("semantic_id")] string SemanticId,
    [property: JsonPropertyName("include_rows")] bool IncludeRows = true);

public sealed record ShapeSheetCellSnapshot(
    string Cell,
    string FormulaU,
    string ResultUniversal);

public sealed record ShapeSheetCellsResult(
    string SemanticId,
    IReadOnlyList<ShapeSheetCellSnapshot> Cells);

public sealed record GeometryRowSnapshot(
    int Row,
    short RowType,
    IReadOnlyDictionary<string, string> Cells);

public sealed record GeometrySectionSnapshot(
    int Section,
    int RowCount,
    IReadOnlyList<GeometryRowSnapshot> Rows);

public sealed record GeometryInspectionResult(
    string SemanticId,
    IReadOnlyList<GeometrySectionSnapshot> Sections);

public sealed record ConnectionPointMutationResult(
    string SemanticId,
    string Name,
    double X,
    double Y);
