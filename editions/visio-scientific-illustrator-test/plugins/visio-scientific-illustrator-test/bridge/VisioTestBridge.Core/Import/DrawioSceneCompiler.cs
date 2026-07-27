using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using VisioTestBridge.Contracts;

namespace VisioTestBridge.Core.Import;

public sealed class DrawioSceneCompiler
{
    public DrawioCompileResult Compile(CompileDrawioSceneCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!Path.IsPathFullyQualified(command.InputPath)
            || !File.Exists(command.InputPath)
            || !Path.GetExtension(command.InputPath).Equals(".drawio", StringComparison.OrdinalIgnoreCase))
            throw Invalid("input_path 必须是已存在的绝对 .drawio 路径。");
        try
        {
            var document = XDocument.Load(command.InputPath, LoadOptions.PreserveWhitespace);
            var (diagramName, graph) = SelectGraph(document, command.DiagramName);
            return CompileGraph(Path.GetFullPath(command.InputPath), diagramName, graph);
        }
        catch (BridgeCommandException) { throw; }
        catch (Exception error) when (error is System.Xml.XmlException or FormatException or InvalidDataException or ArgumentException)
        {
            throw InvalidDocument("无法解析 draw.io 文档。", error);
        }
    }

    private static DrawioCompileResult CompileGraph(string path, string diagramName, XElement graph)
    {
        var warnings = new List<string>();
        var cells = graph.Descendants("mxCell").ToDictionary(
            cell => (string?)cell.Attribute("id") ?? Guid.NewGuid().ToString("N"),
            StringComparer.Ordinal);
        var vertices = cells.Values.Where(cell => (string?)cell.Attribute("vertex") == "1").ToArray();
        var edges = cells.Values.Where(cell => (string?)cell.Attribute("edge") == "1").ToArray();
        var boundsById = vertices.ToDictionary(
            cell => CellId(cell),
            cell => AbsoluteBounds(cell, cells),
            StringComparer.Ordinal);
        var declaredWidth = Number(graph.Attribute("pageWidth"), 0);
        var declaredHeight = Number(graph.Attribute("pageHeight"), 0);
        var maxX = boundsById.Values.Select(box => box.X + box.Width).DefaultIfEmpty(1600).Max();
        var maxY = boundsById.Values.Select(box => box.Y + box.Height).DefaultIfEmpty(900).Max();
        var width = (int)Math.Ceiling(Math.Max(declaredWidth, maxX + 20));
        var height = (int)Math.Ceiling(Math.Max(declaredHeight, maxY + 20));
        var canvas = new SceneCanvas(Math.Max(width, 1), Math.Max(height, 1), 96);
        var nodes = new List<SceneNode>();
        var connectors = new List<SceneConnector>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var gradientCount = 0;
        var rotatedCount = 0;
        var htmlCount = 0;
        var waypointCount = 0;
        var decorativeCount = 0;

        foreach (var cell in vertices)
        {
            var id = StableId(CellId(cell), ids);
            var style = ParseStyle((string?)cell.Attribute("style"));
            var bounds = boundsById[CellId(cell)];
            var value = (string?)cell.Attribute("value") ?? string.Empty;
            var richText = ParseRichText(value, style);
            var isHtml = style.GetValueOrDefault("html") == "1" || value.Contains('<');
            if (isHtml) htmlCount++;
            if (style.TryGetValue("gradientColor", out var gradient) && !IsNone(gradient)) gradientCount++;
            if (style.TryGetValue("rotation", out var rotation) && Number(rotation, 0) != 0) rotatedCount++;
            var shapeStyle = CompileStyle(style);
            var isText = IsText(style, richText.Text);
            var layer = isText ? "labels" : IsPanel(id, bounds, canvas) ? "background" : "content";
            SceneNode node;
            if (isText)
            {
                node = new SceneTextNode(
                    id, bounds, richText.Text, shapeStyle, richText, layer,
                    Role: "label");
            }
            else
            {
                node = new SceneShapeNode(
                    id, bounds, MapShape(style),
                    richText.Text.Length == 0 ? null : richText.Text,
                    shapeStyle,
                    richText.Text.Length == 0 ? null : richText,
                    layer,
                    Role: IsPanel(id, bounds, canvas) ? "background" : null);
            }
            nodes.Add(node);
        }

        foreach (var edge in edges)
        {
            var source = (string?)edge.Attribute("source");
            var target = (string?)edge.Attribute("target");
            var edgeId = StableId(CellId(edge), ids);
            var style = ParseStyle((string?)edge.Attribute("style"));
            if (style.GetValueOrDefault("html") == "1") htmlCount++;
            var points = ReadWaypoints(edge);
            if (source is null || target is null || !boundsById.ContainsKey(source) || !boundsById.ContainsKey(target))
            {
                warnings.Add($"edge {CellId(edge)} 缺少有效 source/target，未编译。");
                continue;
            }
            var mappedSource = StableReference(source, vertices, ids);
            var mappedTarget = StableReference(target, vertices, ids);
            if (IsDecorativeEdge(style, cells[source], cells[target]))
            {
                nodes.Add(DecorativePath(edgeId, boundsById[source], boundsById[target], points, style));
                decorativeCount++;
                continue;
            }
            var (routing, routedPoints) = ResolveRouting(
                style,
                points,
                boundsById[source],
                boundsById[target]);
            var sourceAnchor = CompileAnchor(style, "exitX", "exitY")
                ?? (routedPoints.Count > 0 ? InferAnchor(boundsById[source], routedPoints[0]) : null);
            var targetAnchor = CompileAnchor(style, "entryX", "entryY")
                ?? (routedPoints.Count > 0 ? InferAnchor(boundsById[target], routedPoints[^1]) : null);
            waypointCount += routedPoints.Count;
            connectors.Add(new SceneConnector(
                edgeId,
                mappedSource,
                mappedTarget,
                routing,
                true,
                PlainText((string?)edge.Attribute("value") ?? string.Empty),
                CompileConnectorStyle(style),
                routedPoints.Count == 0 ? null : routedPoints,
                sourceAnchor,
                targetAnchor));
        }

        var regions = BuildRegions(nodes, canvas);
        var colors = vertices.SelectMany(cell => ParseStyle((string?)cell.Attribute("style")).Values)
            .Where(value => Regex.IsMatch(value, "^#[0-9A-Fa-f]{6}$"))
            .Distinct(StringComparer.OrdinalIgnoreCase).Take(64)
            .Select((color, index) => new KeyValuePair<string, string>($"drawio_color_{index + 1}", color))
            .ToDictionary(StringComparer.Ordinal);
        var scene = new ScientificScene(
            Path.GetFileNameWithoutExtension(path),
            canvas,
            colors,
            ["background", "content", "connectors", "labels"],
            nodes,
            connectors,
            regions);
        SceneGraphValidator.Validate(scene);
        return new DrawioCompileResult(
            path,
            diagramName,
            scene,
            new DrawioCompileStatistics(
                vertices.Length,
                edges.Length,
                nodes.Count,
                connectors.Count,
                decorativeCount,
                gradientCount,
                rotatedCount,
                htmlCount,
                waypointCount),
            warnings);
    }

    private static (string Name, XElement Graph) SelectGraph(XDocument document, string? requested)
    {
        if (document.Root?.Name.LocalName == "mxGraphModel")
            return (requested ?? "Page-1", document.Root);
        if (document.Root?.Name.LocalName != "mxfile") throw InvalidDocument("根元素不是 mxfile 或 mxGraphModel。");
        var diagrams = document.Root.Elements("diagram").ToArray();
        if (diagrams.Length == 0) throw InvalidDocument("mxfile 不包含 diagram。");
        var diagram = requested is null
            ? diagrams[0]
            : diagrams.SingleOrDefault(item => (string?)item.Attribute("name") == requested)
                ?? throw InvalidDocument($"找不到 diagram：{requested}");
        var graph = diagram.Elements().FirstOrDefault(element => element.Name.LocalName == "mxGraphModel")
            ?? DecodeGraph(diagram.Value);
        return ((string?)diagram.Attribute("name") ?? "Page-1", graph);
    }

    private static XElement DecodeGraph(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw InvalidDocument("diagram 内容为空。");
        var trimmed = value.Trim();
        if (trimmed.StartsWith('<')) return XDocument.Parse(trimmed).Root!;
        var compressed = Convert.FromBase64String(trimmed);
        using var input = new MemoryStream(compressed);
        using var deflate = new DeflateStream(input, CompressionMode.Decompress);
        using var reader = new StreamReader(deflate, Encoding.UTF8);
        var encoded = reader.ReadToEnd();
        return XDocument.Parse(Uri.UnescapeDataString(encoded)).Root!;
    }

    private static PixelBox AbsoluteBounds(XElement cell, IReadOnlyDictionary<string, XElement> cells)
    {
        var geometry = cell.Elements().FirstOrDefault(item => item.Name.LocalName == "mxGeometry");
        var x = Number(geometry?.Attribute("x"), 0);
        var y = Number(geometry?.Attribute("y"), 0);
        var width = Math.Max(Number(geometry?.Attribute("width"), 1), .01);
        var height = Math.Max(Number(geometry?.Attribute("height"), 1), .01);
        var parent = (string?)cell.Attribute("parent");
        var visited = new HashSet<string>(StringComparer.Ordinal);
        while (parent is not null && cells.TryGetValue(parent, out var parentCell) && visited.Add(parent))
        {
            var parentGeometry = parentCell.Elements().FirstOrDefault(item => item.Name.LocalName == "mxGeometry");
            x += Number(parentGeometry?.Attribute("x"), 0);
            y += Number(parentGeometry?.Attribute("y"), 0);
            parent = (string?)parentCell.Attribute("parent");
        }
        return new PixelBox(x, y, width, height);
    }

    private static ScenePathNode DecorativePath(
        string id,
        PixelBox source,
        PixelBox target,
        IReadOnlyList<PixelPoint> waypoints,
        IReadOnlyDictionary<string, string> style)
    {
        var absolute = new List<PixelPoint>
        {
            new(source.X + source.Width / 2, source.Y + source.Height / 2),
        };
        absolute.AddRange(waypoints);
        absolute.Add(new PixelPoint(target.X + target.Width / 2, target.Y + target.Height / 2));
        var minX = absolute.Min(point => point.X); var minY = absolute.Min(point => point.Y);
        var width = Math.Max(absolute.Max(point => point.X) - minX, 1);
        var height = Math.Max(absolute.Max(point => point.Y) - minY, 1);
        var commands = absolute.Select((point, index) => new ScenePathCommand(
            index == 0 ? "M" : "L", point.X - minX, point.Y - minY)).ToArray();
        return new ScenePathNode(
            id,
            new PixelBox(minX, minY, width, height),
            commands,
            Style: new ShapeStyle(
                Fill: new FillStyle("none"),
                Line: new LineStyle(
                    Color(style, "strokeColor", "#4B5563"),
                    Number(style.GetValueOrDefault("strokeWidth"), 1),
                    Pattern: style.GetValueOrDefault("dashed") == "1" ? 2 : 1)),
            Layer: "connectors",
            Role: "decorative_path");
    }

    private static ShapeStyle CompileStyle(IReadOnlyDictionary<string, string> style)
    {
        var fillColor = style.GetValueOrDefault("fillColor");
        var gradient = style.GetValueOrDefault("gradientColor");
        FillStyle fill = IsNone(fillColor)
            ? new FillStyle("none")
            : !IsNone(gradient)
                ? new FillStyle("gradient", NormalizeColor(fillColor, "#FFFFFF"), NormalizeColor(gradient, "#FFFFFF"),
                    GradientDirection: MapGradientDirection(style.GetValueOrDefault("gradientDirection")))
                : new FillStyle("solid", NormalizeColor(fillColor, "#FFFFFF"));
        var stroke = style.GetValueOrDefault("strokeColor");
        var line = IsNone(stroke)
            ? new LineStyle(Pattern: 0)
            : new LineStyle(
                NormalizeColor(stroke, "#000000"),
                Number(style.GetValueOrDefault("strokeWidth"), 1),
                Pattern: style.GetValueOrDefault("dashed") == "1" ? 2 : 1);
        return new ShapeStyle(
            Fill: fill,
            Line: line,
            TextBlock: new TextBlockStyle(
                MapHorizontal(style.GetValueOrDefault("align")),
                MapVertical(style.GetValueOrDefault("verticalAlign"))),
            Shadow: style.GetValueOrDefault("shadow") == "1" ? new ShadowStyle(true) : null,
            Corner: style.GetValueOrDefault("rounded") == "1"
                ? new CornerStyle(Math.Max(1, Number(style.GetValueOrDefault("arcSize"), 8))) : null,
            RotationDeg: style.TryGetValue("rotation", out var rotation) ? Number(rotation, 0) : null,
            OpacityPercent: style.TryGetValue("opacity", out var opacity) ? Number(opacity, 100) : null);
    }

    private static RichText ParseRichText(string value, IReadOnlyDictionary<string, string> style)
    {
        var plain = PlainText(value);
        var fontStyle = (int)Number(style.GetValueOrDefault("fontStyle"), 0);
        var baseline = new InlineFormat(
            style.GetValueOrDefault("fontFamily"),
            style.TryGetValue("fontSize", out var fontSize) ? DrawioPixelsToPoints(Number(fontSize, 12)) : null,
            NormalizeOptionalColor(style.GetValueOrDefault("fontColor")),
            (fontStyle & 1) != 0,
            (fontStyle & 2) != 0,
            (fontStyle & 4) != 0,
            false,
            false,
            false);
        var text = new StringBuilder();
        var runs = new List<TextRun>();
        if (value.Contains('<'))
        {
            try
            {
                var normalizedEntities = NormalizeHtmlEntitiesForXml(value);
                var normalized = Regex.Replace(normalizedEntities, "<br\\s*/?>", "<br/>", RegexOptions.IgnoreCase);
                var root = XDocument.Parse($"<root>{normalized}</root>").Root!;
                foreach (var node in root.Nodes()) AppendInline(node, baseline, text, runs);
            }
            catch (System.Xml.XmlException)
            {
                text.Append(plain);
                AddRun(runs, 0, plain.Length, baseline);
            }
        }
        else
        {
            text.Append(plain);
            AddRun(runs, 0, plain.Length, baseline);
        }
        return new RichText(
            text.ToString(),
            new TextParagraphStyle(
                MapHorizontal(style.GetValueOrDefault("align")),
                MapVertical(style.GetValueOrDefault("verticalAlign"))),
            runs,
            "shrink_text",
            5);
    }

    private static void AppendInline(
        XNode node,
        InlineFormat format,
        StringBuilder text,
        ICollection<TextRun> runs)
    {
        if (node is XText textNode)
        {
            var start = text.Length;
            var normalized = NormalizeVisioText(textNode.Value);
            text.Append(normalized);
            AddRun(runs, start, normalized.Length, format);
            return;
        }
        if (node is not XElement element) return;
        var name = element.Name.LocalName.ToLowerInvariant();
        if (name == "br")
        {
            text.Append('\n');
            return;
        }
        var nested = name switch
        {
            "b" or "strong" => format with { Bold = true },
            "i" or "em" => format with { Italic = true },
            "u" => format with { Underline = true },
            "s" or "strike" => format with { Strikethrough = true },
            "sub" => format with { Subscript = true, Superscript = false },
            "sup" => format with { Superscript = true, Subscript = false },
            "span" => ApplySpanStyle(format, (string?)element.Attribute("style")),
            _ => format,
        };
        foreach (var child in element.Nodes()) AppendInline(child, nested, text, runs);
    }

    private static InlineFormat ApplySpanStyle(InlineFormat format, string? css)
    {
        if (string.IsNullOrWhiteSpace(css)) return format;
        foreach (var declaration in css.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var colon = declaration.IndexOf(':');
            if (colon < 0) continue;
            var name = declaration[..colon].Trim().ToLowerInvariant();
            var value = declaration[(colon + 1)..].Trim();
            format = name switch
            {
                "color" => format with { Color = NormalizeOptionalColor(value) ?? format.Color },
                "font-family" => format with { FontFamily = value.Trim('"', '\'') },
                "font-size" => format with { FontSizePt = ParseCssSize(value, format.FontSizePt) },
                "font-weight" when value is "bold" or "700" or "800" or "900" => format with { Bold = true },
                "font-style" when value == "italic" => format with { Italic = true },
                "text-decoration" when value.Contains("underline", StringComparison.OrdinalIgnoreCase) => format with { Underline = true },
                "vertical-align" when value == "super" => format with { Superscript = true, Subscript = false },
                "vertical-align" when value == "sub" => format with { Subscript = true, Superscript = false },
                _ => format,
            };
        }
        return format;
    }

    private static double? ParseCssSize(string value, double? fallback)
    {
        var match = Regex.Match(value, "^(?<number>[0-9]+(?:\\.[0-9]+)?)(?<unit>px|pt)?$", RegexOptions.IgnoreCase);
        if (!match.Success) return fallback;
        var size = Number(match.Groups["number"].Value, fallback ?? 12);
        return match.Groups["unit"].Value.Equals("px", StringComparison.OrdinalIgnoreCase) ? size * .75 : size;
    }

    private static void AddRun(ICollection<TextRun> runs, int start, int length, InlineFormat format)
    {
        if (length <= 0) return;
        runs.Add(new TextRun(
            start, length, format.FontFamily, format.FontSizePt, format.Color,
            format.Bold, format.Italic, format.Underline, format.Strikethrough,
            format.Superscript, format.Subscript));
    }

    private sealed record InlineFormat(
        string? FontFamily,
        double? FontSizePt,
        string? Color,
        bool Bold,
        bool Italic,
        bool Underline,
        bool Strikethrough,
        bool Superscript,
        bool Subscript);

    private static string PlainText(string value)
    {
        var withLines = Regex.Replace(value, "<br\\s*/?>", "\n", RegexOptions.IgnoreCase);
        return NormalizeVisioText(
            DecodeHtml(Regex.Replace(withLines, "<[^>]+>", string.Empty)).Trim());
    }

    private static string DecodeHtml(string value) => WebUtility.HtmlDecode(
        value.Replace("&vellip;", "\u22EE", StringComparison.OrdinalIgnoreCase));

    private static string NormalizeHtmlEntitiesForXml(string value) => Regex.Replace(
        value,
        "&(?<name>[A-Za-z][A-Za-z0-9]+);",
        match => match.Groups["name"].Value.ToLowerInvariant() is "amp" or "lt" or "gt" or "quot" or "apos"
            ? match.Value
            : WebUtility.HtmlDecode(match.Value),
        RegexOptions.CultureInvariant);

    private static string NormalizeVisioText(string value) =>
        Regex.Replace(value, "\u00B7{3,}", match => new string('.', match.Length));

    private static double DrawioPixelsToPoints(double pixels) => pixels * 72d / 96d;

    private static ConnectorStyle CompileConnectorStyle(IReadOnlyDictionary<string, string> style) => new(
        NormalizeOptionalColor(style.GetValueOrDefault("strokeColor")),
        style.TryGetValue("strokeWidth", out var width) ? Number(width, 1) : null,
        style.GetValueOrDefault("dashed") == "1",
        MapArrow(style.GetValueOrDefault("startArrow")),
        MapArrow(style.GetValueOrDefault("endArrow")));

    private static ConnectorAnchor? CompileAnchor(IReadOnlyDictionary<string, string> style, string xName, string yName)
    {
        if (!style.TryGetValue(xName, out var x) || !style.TryGetValue(yName, out var y)) return null;
        return new ConnectorAnchor("normalized", X: Number(x, 0.5), Y: Number(y, 0.5));
    }

    private static IReadOnlyList<PixelPoint> ReadWaypoints(XElement edge) => edge.Descendants()
        .Where(element => element.Name.LocalName == "mxPoint"
            && element.Parent?.Name.LocalName == "Array"
            && (string?)element.Parent.Attribute("as") == "points")
        .Select(point => new PixelPoint(Number(point.Attribute("x"), 0), Number(point.Attribute("y"), 0)))
        .ToArray();

    private static IReadOnlyList<SceneRegion> BuildRegions(IReadOnlyList<SceneNode> nodes, SceneCanvas canvas)
    {
        var candidates = nodes.Where(node => node.Role == "background"
            && node.Bounds.Width >= canvas.WidthPx * .15
            && node.Bounds.Height >= canvas.HeightPx * .35).Take(32).ToArray();
        var panels = candidates.Where(panel => !candidates.Any(container =>
            !ReferenceEquals(panel, container)
            && container.Bounds.Width * container.Bounds.Height > panel.Bounds.Width * panel.Bounds.Height
            && Contains(container.Bounds, panel.Bounds))).Take(16).ToArray();
        return panels.Length == 0
            ? [new SceneRegion("full", new PixelBox(0, 0, canvas.WidthPx, canvas.HeightPx), true)]
            : panels.Select((panel, index) => new SceneRegion($"region_{index + 1}", panel.Bounds, true)).ToArray();
    }

    private static bool Contains(PixelBox outer, PixelBox inner) =>
        inner.X >= outer.X && inner.Y >= outer.Y
        && inner.X + inner.Width <= outer.X + outer.Width
        && inner.Y + inner.Height <= outer.Y + outer.Height;

    private static Dictionary<string, string> ParseStyle(string? input)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(input)) return result;
        foreach (var part in input.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var equals = part.IndexOf('=');
            if (equals < 0) result[part] = "1";
            else result[part[..equals]] = part[(equals + 1)..];
        }
        return result;
    }

    private static string MapShape(IReadOnlyDictionary<string, string> style)
    {
        var shape = style.GetValueOrDefault("shape");
        if (style.ContainsKey("ellipse") || shape == "ellipse") return "ellipse";
        return shape switch
        {
            "cylinder" => "cylinder", "cloud" => "cloud", "hexagon" => "hexagon",
            "rhombus" => "diamond", "triangle" => "triangle", "parallelogram" => "parallelogram",
            _ => style.GetValueOrDefault("rounded") == "1" ? "rounded_rectangle" : "rectangle",
        };
    }

    private static bool IsText(IReadOnlyDictionary<string, string> style, string text) => style.ContainsKey("text")
        || text.Length > 0
        && IsNone(style.GetValueOrDefault("fillColor"))
        && IsNone(style.GetValueOrDefault("strokeColor"));

    private static bool IsPanel(string id, PixelBox box, SceneCanvas canvas) =>
        id.Contains("panel", StringComparison.OrdinalIgnoreCase)
        || id.EndsWith("_box", StringComparison.OrdinalIgnoreCase) && box.Width > canvas.WidthPx * .2;

    private static bool IsDecorativeEdge(
        IReadOnlyDictionary<string, string> style,
        XElement source,
        XElement target) =>
        IsNone(style.GetValueOrDefault("endArrow"))
        && IsNone(style.GetValueOrDefault("startArrow"))
        && (style.GetValueOrDefault("edgeStyle") == "none"
            || IsInvisibleAnchor(source) && IsInvisibleAnchor(target));

    private static bool IsInvisibleAnchor(XElement cell)
    {
        if (!string.IsNullOrEmpty((string?)cell.Attribute("value"))) return false;
        var style = ParseStyle((string?)cell.Attribute("style"));
        return IsNone(style.GetValueOrDefault("fillColor"))
            && IsNone(style.GetValueOrDefault("strokeColor"))
            && Number(style.GetValueOrDefault("opacity"), 100) == 0;
    }

    private static (string Routing, IReadOnlyList<PixelPoint> Points) ResolveRouting(
        IReadOnlyDictionary<string, string> style,
        IReadOnlyList<PixelPoint> points,
        PixelBox source,
        PixelBox target)
    {
        if (points.Count > 0) return ("polyline", points);
        if (style.GetValueOrDefault("curved") == "1") return ("dynamic", []);
        if (style.GetValueOrDefault("edgeStyle") != "orthogonalEdgeStyle") return ("straight", []);

        var sourceCenter = new PixelPoint(source.X + source.Width / 2, source.Y + source.Height / 2);
        var targetCenter = new PixelPoint(target.X + target.Width / 2, target.Y + target.Height / 2);
        if (Math.Abs(sourceCenter.X - targetCenter.X) <= 40
            || Math.Abs(sourceCenter.Y - targetCenter.Y) <= 40)
            return ("straight", []);

        if (Math.Abs(sourceCenter.X - targetCenter.X) >= Math.Abs(sourceCenter.Y - targetCenter.Y))
        {
            var middleX = (sourceCenter.X + targetCenter.X) / 2;
            return ("polyline",
            [
                new PixelPoint(middleX, sourceCenter.Y),
                new PixelPoint(middleX, targetCenter.Y),
            ]);
        }

        var middleY = (sourceCenter.Y + targetCenter.Y) / 2;
        return ("polyline",
        [
            new PixelPoint(sourceCenter.X, middleY),
            new PixelPoint(targetCenter.X, middleY),
        ]);
    }

    private static ConnectorAnchor InferAnchor(PixelBox box, PixelPoint toward)
    {
        var centerX = box.X + box.Width / 2;
        var centerY = box.Y + box.Height / 2;
        var dx = toward.X - centerX;
        var dy = toward.Y - centerY;
        if (Math.Abs(dx) >= Math.Abs(dy))
            return new ConnectorAnchor(
                "normalized",
                X: dx >= 0 ? 1 : 0,
                Y: Math.Clamp((toward.Y - box.Y) / box.Height, 0, 1));
        return new ConnectorAnchor(
            "normalized",
            X: Math.Clamp((toward.X - box.X) / box.Width, 0, 1),
            Y: dy >= 0 ? 1 : 0);
    }

    private static string? MapArrow(string? arrow) => arrow switch
    {
        null or "" or "none" => "none", "block" or "classic" => "triangle",
        "open" => "open", "diamond" => "diamond", "oval" => "oval", _ => "triangle",
    };

    private static int MapGradientDirection(string? direction) => direction switch
    {
        "north" => 0, "east" => 2, "south" => 4, "west" => 6, _ => 0,
    };

    private static string? MapHorizontal(string? value) => value switch
    {
        "left" => "left", "right" => "right", "center" => "center", "justify" => "justify", _ => null,
    };

    private static string? MapVertical(string? value) => value switch
    {
        "top" => "top", "bottom" => "bottom", "middle" => "middle", _ => null,
    };

    private static string CellId(XElement cell) => (string?)cell.Attribute("id") ?? throw InvalidDocument("mxCell 缺少 id。");

    private static string StableId(string source, ISet<string> used)
    {
        var safe = source.Length <= 100 ? source : source[..80] + "_" + Hash(source);
        var candidate = safe;
        var index = 2;
        while (!used.Add(candidate)) candidate = $"{safe}_{index++}";
        return candidate;
    }

    private static string StableReference(string source, IReadOnlyList<XElement> vertices, ISet<string> used)
    {
        var order = vertices.Select(CellId).ToList();
        var index = order.IndexOf(source);
        if (index < 0) return source;
        var raw = order[index];
        return raw.Length <= 100 ? raw : raw[..80] + "_" + Hash(raw);
    }

    private static string Hash(string input) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant()[..12];

    private static double Number(XAttribute? attribute, double fallback) => Number(attribute?.Value, fallback);
    private static double Number(string? value, double fallback) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && double.IsFinite(number)
            ? number : fallback;
    private static bool IsNone(string? value) => string.IsNullOrWhiteSpace(value) || value == "none";
    private static string Color(IReadOnlyDictionary<string, string> style, string name, string fallback) =>
        NormalizeColor(style.GetValueOrDefault(name), fallback);
    private static string NormalizeColor(string? value, string fallback) =>
        value is not null && Regex.IsMatch(value, "^#[0-9A-Fa-f]{6}$") ? value.ToUpperInvariant() : fallback;
    private static string? NormalizeOptionalColor(string? value) =>
        value is not null && Regex.IsMatch(value, "^#[0-9A-Fa-f]{6}$") ? value.ToUpperInvariant() : null;

    private static BridgeCommandException Invalid(string message) =>
        new(BridgeErrorCodes.InvalidArgument, message);
    private static BridgeCommandException InvalidDocument(string message, Exception? error = null) =>
        new(BridgeErrorCodes.InvalidDrawioDocument, message, details: error is null ? null : new { error = error.Message });
}
