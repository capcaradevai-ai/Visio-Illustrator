using VisioBridge.Contracts;
using VisioBridge.Core;
using VisioBridge.Core.Shapes;

namespace VisioBridge.Tests;

public sealed class FreeformBuilderTests
{
    private static readonly PixelBox Bounds = new(100, 80, 100, 100);

    [Fact]
    public void Open_waveform_compiles_to_relative_move_and_cubic_rows()
    {
        var command = Path(
            new ScenePathCommand("M", 0, 50),
            new ScenePathCommand("C", 50, 50, 15, 10, 35, 90),
            new ScenePathCommand("C", 100, 50, 65, 10, 85, 90));

        var geometry = PathGeometryCompiler.Compile(command);

        var section = Assert.Single(geometry.Sections);
        Assert.True(section.NoFill);
        Assert.Equal(
            [GeometryRowKind.RelMoveTo, GeometryRowKind.RelCubBezTo, GeometryRowKind.RelCubBezTo],
            section.Rows.Select(row => row.Kind));
        Assert.Equal(0, section.Rows[0].X, 6);
        Assert.Equal(0.5, section.Rows[0].Y, 6);
        Assert.Equal(0.15, section.Rows[1].A!.Value, 6);
        Assert.Equal(0.9, section.Rows[1].B!.Value, 6);
        Assert.Equal(0.35, section.Rows[1].C!.Value, 6);
        Assert.Equal(0.1, section.Rows[1].D!.Value, 6);
    }

    [Fact]
    public void Closed_silhouette_emits_an_editable_line_back_to_subpath_start()
    {
        var command = new AddPathCommand(
            "person",
            Bounds,
            [new PathSection(
                [
                    new ScenePathCommand("M", 50, 5),
                    new ScenePathCommand("L", 82, 45),
                    new ScenePathCommand("C", 50, 95, 78, 75, 64, 94),
                    new ScenePathCommand("Z"),
                ],
                Closed: true,
                Filled: true)]);

        var section = Assert.Single(PathGeometryCompiler.Compile(command).Sections);

        Assert.False(section.NoFill);
        Assert.Equal(GeometryRowKind.RelLineTo, section.Rows[^1].Kind);
        Assert.Equal(0.5, section.Rows[^1].X, 6);
        Assert.Equal(0.95, section.Rows[^1].Y, 6);
    }

    [Fact]
    public void Svg_arc_compiles_to_a_relative_elliptical_arc_row()
    {
        var command = Path(
            new ScenePathCommand("M", 10, 50),
            new ScenePathCommand(
                "A",
                X: 90,
                Y: 50,
                Rx: 40,
                Ry: 30,
                RotationDeg: 0,
                LargeArc: false,
                Sweep: true));

        var arc = PathGeometryCompiler.Compile(command).Sections[0].Rows[1];

        Assert.Equal(GeometryRowKind.RelEllipticalArcTo, arc.Kind);
        Assert.Equal(0.9, arc.X, 6);
        Assert.Equal(0.5, arc.Y, 6);
        Assert.Equal(0.5, arc.A!.Value, 6);
        Assert.NotEqual(0.5, arc.B!.Value, 6);
        Assert.Equal(0, arc.C!.Value, 6);
        Assert.Equal(4d / 3d, arc.D!.Value, 6);
    }

    [Fact]
    public void Multiple_geometry_sections_remain_separate()
    {
        var command = new AddPathCommand(
            "double-wave",
            Bounds,
            [
                new PathSection([
                    new ScenePathCommand("M", 0, 25),
                    new ScenePathCommand("L", 100, 25),
                ]),
                new PathSection([
                    new ScenePathCommand("M", 0, 75),
                    new ScenePathCommand("L", 100, 75),
                ]),
            ]);

        var geometry = PathGeometryCompiler.Compile(command);

        Assert.Equal(2, geometry.Sections.Count);
        Assert.Equal(0.75, geometry.Sections[0].Rows[0].Y, 6);
        Assert.Equal(0.25, geometry.Sections[1].Rows[0].Y, 6);
    }

    [Fact]
    public void First_command_must_be_move()
    {
        var command = Path(new ScenePathCommand("L", 100, 50));

        var error = Assert.Throws<BridgeCommandException>(() => PathGeometryCompiler.Compile(command));

        Assert.Equal(BridgeErrorCodes.InvalidArgument, error.Code);
        Assert.Contains("首命令", error.Message);
    }

    [Fact]
    public void Filled_section_must_be_closed()
    {
        var command = new AddPathCommand(
            "open-fill",
            Bounds,
            [new PathSection([
                new ScenePathCommand("M", 10, 10),
                new ScenePathCommand("L", 90, 90),
            ], Filled: true)]);

        var error = Assert.Throws<BridgeCommandException>(() => PathGeometryCompiler.Compile(command));

        Assert.Equal(BridgeErrorCodes.InvalidArgument, error.Code);
        Assert.Contains("填充路径必须闭合", error.Message);
    }

    [Fact]
    public void Path_limits_are_enforced_before_geometry_is_created()
    {
        var tooManySections = Enumerable.Range(0, 33)
            .Select(_ => new PathSection([new ScenePathCommand("M", 0, 0)]))
            .ToArray();
        var tooManyCommands = Enumerable.Range(0, 2001)
            .Select(index => index == 0
                ? new ScenePathCommand("M", 0, 0)
                : new ScenePathCommand("L", index % 100, index % 100))
            .ToArray();

        var sectionError = Assert.Throws<BridgeCommandException>(() =>
            PathGeometryCompiler.Compile(new AddPathCommand("sections", Bounds, tooManySections)));
        var commandError = Assert.Throws<BridgeCommandException>(() =>
            PathGeometryCompiler.Compile(new AddPathCommand(
                "commands",
                Bounds,
                [new PathSection(tooManyCommands)])));

        Assert.Equal(BridgeErrorCodes.InvalidArgument, sectionError.Code);
        Assert.Equal(BridgeErrorCodes.InvalidArgument, commandError.Code);
    }

    private static AddPathCommand Path(params ScenePathCommand[] commands) =>
        new("wave", Bounds, [new PathSection(commands)]);
}
