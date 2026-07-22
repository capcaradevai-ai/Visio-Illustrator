using VisioBridge.Contracts;
using VisioBridge.Core.Inspection;

namespace VisioBridge.Core.Shapes;

internal sealed record ComponentPrimitive(
    string Suffix,
    string Kind,
    PixelBox Bounds,
    string? Shape = null,
    string? Label = null,
    ShapeStyle? Style = null,
    IReadOnlyList<PathSection>? Sections = null,
    string? ImagePath = null);

internal sealed record ScientificComponentPlan(IReadOnlyList<ComponentPrimitive> Primitives);

internal static class ScientificComponentPlanner
{
    private const string DefaultBlue = "#2F75B5";
    private const string DefaultLight = "#DCEBFA";

    public static ScientificComponentPlan Create(AddScientificComponentCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (string.IsNullOrWhiteSpace(command.SemanticId)) throw Invalid("semantic_id 不能为空。");
        ValidateBounds(command.Bounds);
        var options = command.Options ?? new ScientificComponentOptions();
        var primary = options.PrimaryColor ?? DefaultBlue;
        var secondary = options.SecondaryColor ?? DefaultLight;
        var primitives = command.Component switch
        {
            "number_badge" => NumberBadge(command.Bounds, options, primary, secondary),
            "router" => Router(command.Bounds, primary, secondary),
            "human_silhouette" => Human(command.Bounds, primary),
            "joint_skeleton" => Skeleton(command.Bounds, options, primary, secondary),
            "video_frame_stack" => VideoFrames(command.Bounds, options, primary, secondary),
            "tensor_cube" => Tensor(command.Bounds, options, primary, secondary),
            "latent_vector" => Latent(command.Bounds, options, primary, secondary),
            "waveform" => Waveform(command.Bounds, options, primary),
            "snowflake" => Snowflake(command.Bounds, primary),
            "wireless_link" => Wireless(command.Bounds, options, primary),
            "stage_panel" => StagePanel(command.Bounds, options, primary, secondary),
            _ => throw Invalid($"不支持科学组件：{command.Component}"),
        };
        return new ScientificComponentPlan(primitives);
    }

    private static List<ComponentPrimitive> NumberBadge(PixelBox b, ScientificComponentOptions o, string p, string s) =>
    [
        Shape("outer", "ellipse", B(b, .06, .06, .88, .88), null, Fill(p, "#FFFFFF", shadow: true)),
        Shape("inner", "ellipse", B(b, .17, .17, .66, .66), null, Fill(s, p)),
        Shape("label", "text", B(b, .2, .2, .6, .6), o.Label ?? "1", Text(p, Math.Max(12, b.Height * .23))),
    ];

    private static List<ComponentPrimitive> Router(PixelBox b, string p, string s)
    {
        var result = new List<ComponentPrimitive>
        {
            Shape("body", "rounded_rectangle", B(b, .08, .38, .84, .45), null, Fill(s, p, shadow: true)),
            Shape("label", "text", B(b, .24, .48, .52, .18), "Router", Text(p, 10)),
        };
        result.Add(Line("antenna_left", b.X + b.Width * .25, b.Y + b.Height * .45, b.X + b.Width * .18, b.Y + b.Height * .08, p, 2));
        result.Add(Line("antenna_right", b.X + b.Width * .75, b.Y + b.Height * .45, b.X + b.Width * .82, b.Y + b.Height * .08, p, 2));
        for (var index = 0; index < 3; index++)
            result.Add(Shape($"led_{index + 1}", "ellipse", B(b, .22 + index * .12, .68, .055, .055), null, Fill(index == 0 ? "#39A84A" : "#74B9E6", p)));
        result.Add(Curve("signal", B(b, .35, .05, .3, .28),
            [new("M", 0, 70), new("C", 90, 70, 20, 0, 70, 0)], p, 1.5));
        return result;
    }

    private static List<ComponentPrimitive> Human(PixelBox b, string p) =>
    [
        Shape("head", "ellipse", B(b, .39, .02, .22, .2), null, Fill(p, p)),
        Curve("torso", B(b, .25, .2, .5, .4), [new("M", 50, 0), new("C", 50, 100, 20, 28, 80, 28)], p, 8),
        Line("arm_left", b.X + b.Width * .43, b.Y + b.Height * .35, b.X + b.Width * .08, b.Y + b.Height * .58, p, 7),
        Line("arm_right", b.X + b.Width * .57, b.Y + b.Height * .35, b.X + b.Width * .92, b.Y + b.Height * .58, p, 7),
        Line("leg_left", b.X + b.Width * .46, b.Y + b.Height * .57, b.X + b.Width * .24, b.Y + b.Height * .98, p, 8),
        Line("leg_right", b.X + b.Width * .54, b.Y + b.Height * .57, b.X + b.Width * .76, b.Y + b.Height * .98, p, 8),
    ];

    private static List<ComponentPrimitive> Skeleton(PixelBox b, ScientificComponentOptions o, string p, string s)
    {
        var joints = new (string Name, double X, double Y)[]
        {
            ("head", .5, .08), ("neck", .5, .24), ("shoulder_l", .28, .3), ("shoulder_r", .72, .3),
            ("hip", .5, .55), ("hand_l", .08, .55), ("hand_r", .92, .55),
            ("knee_l", .36, .75), ("knee_r", .64, .75), ("foot_l", .24, .96), ("foot_r", .76, .96),
        };
        var links = new (int A, int B)[] { (0,1),(1,2),(1,3),(1,4),(2,5),(3,6),(4,7),(4,8),(7,9),(8,10) };
        var result = new List<ComponentPrimitive>();
        for (var index = 0; index < links.Length; index++)
        {
            var a = joints[links[index].A]; var c = joints[links[index].B];
            result.Add(Line($"bone_{index + 1}", b.X + b.Width * a.X, b.Y + b.Height * a.Y,
                b.X + b.Width * c.X, b.Y + b.Height * c.Y, index % 2 == 0 ? p : "#E27D35", 3));
        }
        for (var index = 0; index < joints.Length; index++)
            result.Add(Shape($"joint_{joints[index].Name}", "ellipse",
                new PixelBox(b.X + b.Width * joints[index].X - 6, b.Y + b.Height * joints[index].Y - 6, 12, 12),
                o.ShowLabels == true ? (index + 1).ToString() : null, Fill(s, p)));
        return result;
    }

    private static List<ComponentPrimitive> VideoFrames(PixelBox b, ScientificComponentOptions o, string p, string s)
    {
        var count = Range(o.Count, 3, 2, 8, "count");
        var result = new List<ComponentPrimitive>();
        for (var index = count - 1; index >= 0; index--)
            result.Add(Shape($"frame_{count - index}", "rounded_rectangle",
                new PixelBox(b.X + index * b.Width * .04, b.Y + index * b.Height * .04,
                    b.Width * .82, b.Height * .78), null, Fill(index == 0 ? "#FFFFFF" : s, p, shadow: index == count - 1)));
        if (!string.IsNullOrWhiteSpace(o.ImagePath))
            result.Add(new ComponentPrimitive("image", "image", B(b, .06, .08, .7, .57), ImagePath: o.ImagePath));
        result.Add(Shape("caption", "text", B(b, .08, .68, .66, .13), o.Title ?? "Video frames", Text(p, 9)));
        return result;
    }

    private static List<ComponentPrimitive> Tensor(PixelBox b, ScientificComponentOptions o, string p, string s)
    {
        var rows = Range(o.Rows, 5, 2, 100, "rows");
        var columns = Range(o.Columns, 5, 2, 100, "columns");
        var front = B(b, .08, .2, .68, .7);
        var result = new List<ComponentPrimitive>
        {
            Shape("front", "rectangle", front, null, Fill(s, p)),
            PercentPath("top_face", B(b, .08, .05, .84, .2), [new("M",0,75),new("L",82,0),new("L",100,25),new("L",18,100),new("Z")], p, 1, true),
            PercentPath("side_face", B(b, .76, .2, .16, .7), [new("M",0,0),new("L",100,0),new("L",100,80),new("L",0,100),new("Z")], p, 1, true),
        };
        for (var row = 1; row <= rows; row++)
        {
            var y = front.Y + front.Height * row / (rows + 1);
            result.Add(Line($"row_{row}", front.X, y, front.X + front.Width, y, p, .7));
        }
        for (var column = 1; column <= columns; column++)
        {
            var x = front.X + front.Width * column / (columns + 1);
            result.Add(Line($"column_{column}", x, front.Y, x, front.Y + front.Height, p, .7));
        }
        return result;
    }

    private static List<ComponentPrimitive> Latent(PixelBox b, ScientificComponentOptions o, string p, string s)
    {
        var count = Range(o.Count, 5, 3, 30, "count");
        var result = new List<ComponentPrimitive> { Shape("frame", "rounded_rectangle", b, null, Fill("#FFFFFF", p)) };
        for (var index = 0; index < count; index++)
            result.Add(Shape($"node_{index + 1}", "ellipse",
                B(b, .39, .1 + .68 * index / Math.Max(1, count - 1), .22, .09), null, Fill(s, p)));
        result.Add(Shape("ellipsis", "text", B(b, .35, .79, .3, .12), "⋮", Text(p, 14)));
        return result;
    }

    private static List<ComponentPrimitive> Waveform(PixelBox b, ScientificComponentOptions o, string p)
    {
        var count = Range(o.Count, 3, 1, 24, "count");
        var result = new List<ComponentPrimitive>();
        for (var index = 0; index < count; index++)
        {
            var top = b.Y + b.Height * index / count;
            result.Add(Curve($"wave_{index + 1}", new PixelBox(b.X, top, b.Width, b.Height / count * .8),
                [new("M",0,50),new("C",50,50,12,0,25,100),new("C",100,50,62,0,78,100)], p, 1.4));
        }
        return result;
    }

    private static List<ComponentPrimitive> Snowflake(PixelBox b, string p)
    {
        var result = new List<ComponentPrimitive>();
        var cx = b.X + b.Width / 2; var cy = b.Y + b.Height / 2;
        var radius = Math.Min(b.Width, b.Height) * .46;
        for (var axis = 0; axis < 6; axis++)
        {
            var angle = Math.PI * axis / 3;
            var ex = cx + Math.Cos(angle) * radius; var ey = cy + Math.Sin(angle) * radius;
            result.Add(Line($"axis_{axis + 1}", cx, cy, ex, ey, p, 2));
            for (var branch = 1; branch <= 2; branch++)
            {
                var t = .55 + branch * .14;
                var bx = cx + Math.Cos(angle) * radius * t; var by = cy + Math.Sin(angle) * radius * t;
                foreach (var direction in new[] { -1, 1 })
                {
                    var branchAngle = angle + direction * Math.PI * 2 / 3;
                    result.Add(Line($"branch_{axis + 1}_{branch}_{direction + 2}", bx, by,
                        bx + Math.Cos(branchAngle) * radius * .2, by + Math.Sin(branchAngle) * radius * .2, p, 1.3));
                }
            }
        }
        return result;
    }

    private static List<ComponentPrimitive> Wireless(PixelBox b, ScientificComponentOptions o, string p)
    {
        var count = Range(o.Count, 3, 2, 8, "count");
        var result = new List<ComponentPrimitive>();
        for (var index = 0; index < count; index++)
        {
            var inset = index * .12;
            result.Add(Curve($"arc_{index + 1}", B(b, inset, inset, 1 - inset * 2, 1 - inset * 2),
                [new("M",0,80),new("C",100,80,20,0,80,0)], p, 2));
        }
        return result;
    }

    private static List<ComponentPrimitive> StagePanel(PixelBox b, ScientificComponentOptions o, string p, string s) =>
    [
        Shape("panel", "rounded_rectangle", b, null, Fill("#FFFFFF", p, shadow: true)),
        Shape("header", "rounded_rectangle", B(b, .015, .02, .97, .22), null, Fill(s, p)),
        Shape("title", "text", B(b, .06, .055, .88, .13), o.Title ?? "Stage", Text(p, 12)),
        Line("divider", b.X + b.Width * .04, b.Y + b.Height * .27, b.X + b.Width * .96, b.Y + b.Height * .27, p, 1),
    ];

    private static ComponentPrimitive Shape(string suffix, string shape, PixelBox bounds, string? label, ShapeStyle style) =>
        new(suffix, "shape", bounds, shape, label, style);

    private static ComponentPrimitive Line(string suffix, double x1, double y1, double x2, double y2, string color, double width)
    {
        var x = Math.Min(x1, x2); var y = Math.Min(y1, y2);
        var w = Math.Max(Math.Abs(x2 - x1), 1); var h = Math.Max(Math.Abs(y2 - y1), 1);
        return Path(suffix, new PixelBox(x, y, w, h),
            [new("M", x1 - x, y1 - y), new("L", x2 - x, y2 - y)], color, width);
    }

    private static ComponentPrimitive Curve(string suffix, PixelBox bounds, IReadOnlyList<ScenePathCommand> commands, string color, double width) =>
        PercentPath(suffix, bounds, commands, color, width);

    private static ComponentPrimitive PercentPath(
        string suffix,
        PixelBox bounds,
        IReadOnlyList<ScenePathCommand> commands,
        string color,
        double width,
        bool filled = false) =>
        Path(suffix, bounds, commands.Select(command => command with
        {
            X = command.X * bounds.Width / 100,
            Y = command.Y * bounds.Height / 100,
            X1 = command.X1 * bounds.Width / 100,
            Y1 = command.Y1 * bounds.Height / 100,
            X2 = command.X2 * bounds.Width / 100,
            Y2 = command.Y2 * bounds.Height / 100,
            Rx = command.Rx * bounds.Width / 100,
            Ry = command.Ry * bounds.Height / 100,
        }).ToArray(), color, width, filled);

    private static ComponentPrimitive Path(string suffix, PixelBox bounds, IReadOnlyList<ScenePathCommand> commands, string color, double width, bool filled = false) =>
        new(suffix, "path", bounds, Style: new ShapeStyle(
            Fill: new FillStyle(filled ? "solid" : "none", filled ? "#DCEBFA" : null),
            Line: new LineStyle(color, width, Cap: "round")),
            Sections: [new PathSection(commands, Closed: filled, Filled: filled)]);

    private static ShapeStyle Fill(string color, string line, bool shadow = false) => new(
        Fill: new FillStyle("gradient", color, "#FFFFFF", 0, 15, 90),
        Line: new LineStyle(line, 1.2, Cap: "round"),
        Shadow: shadow ? new ShadowStyle(true, "#808080", 60, 2, 2) : null);

    private static ShapeStyle Text(string color, double size) => new(
        FontColor: color,
        FontSizePt: size);

    private static PixelBox B(PixelBox b, double x, double y, double width, double height) =>
        new(b.X + b.Width * x, b.Y + b.Height * y, b.Width * width, b.Height * height);

    private static int Range(int? value, int fallback, int min, int max, string name)
    {
        var actual = value ?? fallback;
        if (actual < min || actual > max) throw Invalid($"{name} 必须在 {min}–{max} 范围内。");
        return actual;
    }

    private static void ValidateBounds(PixelBox b)
    {
        if (!double.IsFinite(b.X) || !double.IsFinite(b.Y) || !double.IsFinite(b.Width) || !double.IsFinite(b.Height)
            || b.Width <= 0 || b.Height <= 0) throw Invalid("bounds 必须是有限的正尺寸。");
    }

    private static BridgeCommandException Invalid(string message) => new(BridgeErrorCodes.InvalidArgument, message);
}

internal sealed class ScientificComponentFactory
{
    private readonly ShapeFactory _shapes;
    private readonly FreeformBuilder _paths;
    private readonly VisioGroupingService _groups;
    private readonly ShapeRepository _repository;
    private readonly PageInspector _inspector;

    public ScientificComponentFactory(
        ShapeFactory shapes,
        FreeformBuilder paths,
        VisioGroupingService groups,
        ShapeRepository repository,
        PageInspector inspector)
    {
        _shapes = shapes;
        _paths = paths;
        _groups = groups;
        _repository = repository;
        _inspector = inspector;
    }

    public ScientificComponentMutationResult Add(AddScientificComponentCommand command)
    {
        var plan = ScientificComponentPlanner.Create(command);
        var childIds = new List<string>(plan.Primitives.Count);
        foreach (var primitive in plan.Primitives)
        {
            var id = $"{command.SemanticId}__{primitive.Suffix}";
            if (primitive.Kind == "shape")
                _shapes.Add(new AddShapeCommand(id, primitive.Shape!, primitive.Bounds, primitive.Label, primitive.Style));
            else if (primitive.Kind == "path")
                _paths.Add(new AddPathCommand(id, primitive.Bounds, primitive.Sections!, primitive.Style));
            else
                _shapes.AddImage(new AddImageCommand(id, primitive.ImagePath!, primitive.Bounds));
            childIds.Add(id);
        }
        var group = _groups.Group(new GroupShapesCommand(command.SemanticId, childIds));
        using var handle = _repository.Resolve(command.SemanticId);
        var shape = ((VisioShapeHandle)handle).Shape;
        ConnectionPointService.EnsureStandardAnchorsForShape(shape);
        return new ScientificComponentMutationResult(
            command.SemanticId,
            command.Component,
            group.ShapeId,
            childIds,
            childIds.Count,
            _inspector.ReadBounds(shape),
            ConnectionPointService.StandardAnchors.Keys.ToArray());
    }
}
