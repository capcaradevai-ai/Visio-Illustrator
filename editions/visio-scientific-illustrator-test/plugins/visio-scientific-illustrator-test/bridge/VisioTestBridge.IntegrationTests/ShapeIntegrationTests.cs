using VisioTestBridge.Contracts;
using System.IO.Compression;
using System.Globalization;
using System.Xml.Linq;

namespace VisioTestBridge.IntegrationTests;

public sealed class ShapeIntegrationTests
{
    [Trait("Category", "VisioIntegration")]
    [Fact]
    public void Editable_geometry_sections_survive_path_update_and_vsdx_save()
    {
        var fixture = VisioFixture.StartVisible();
        var output = Path.Combine(Path.GetTempPath(), $"visio-path-{Guid.NewGuid():N}.vsdx");
        try
        {
            var added = fixture.AddPath(new AddPathCommand(
                "scientific-path",
                new PixelBox(100, 100, 800, 500),
                [
                    WaveSection(110),
                    WaveSection(210),
                    new PathSection(
                        [
                            new ScenePathCommand("M", 550, 80),
                            new ScenePathCommand("A", 650, 80, Rx: 50, Ry: 50, RotationDeg: 0, LargeArc: false, Sweep: true),
                            new ScenePathCommand("L", 620, 350),
                            new ScenePathCommand("L", 580, 350),
                            new ScenePathCommand("Z"),
                        ],
                        Closed: true,
                        Filled: true),
                ],
                new ShapeStyle(
                    Fill: new FillStyle("solid", Color: "#D9E8FB"),
                    Line: new LineStyle(Color: "#245B91", WidthPt: 1.5))));

            var updated = fixture.UpdatePath(new UpdatePathCommand(
                "scientific-path",
                Sections:
                [
                    WaveSection(100),
                    WaveSection(200),
                    new PathSection(
                        [
                            new ScenePathCommand("M", 550, 70),
                            new ScenePathCommand("A", 650, 70, Rx: 50, Ry: 50, RotationDeg: 0, LargeArc: false, Sweep: true),
                            new ScenePathCommand("L", 625, 360),
                            new ScenePathCommand("L", 575, 360),
                            new ScenePathCommand("Z"),
                        ],
                        Closed: true,
                        Filled: true),
                ]));

            Assert.Equal(added.ShapeId, updated.ShapeId);
            Assert.Equal(3, updated.SectionCount);
            fixture.Save(output);
            fixture.Dispose();

            using var archive = ZipFile.OpenRead(output);
            var page = archive.GetEntry("visio/pages/page1.xml");
            Assert.NotNull(page);
            using var reader = new StreamReader(page.Open());
            var xml = XDocument.Parse(reader.ReadToEnd());
            var geometrySections = xml.Descendants()
                .Where(element => element.Name.LocalName == "Section"
                    && ((string?)element.Attribute("N"))?.StartsWith("Geometry", StringComparison.Ordinal) == true)
                .ToArray();
            var rowTypes = geometrySections
                .SelectMany(section => section.Elements().Where(element => element.Name.LocalName == "Row"))
                .Select(row => (string?)row.Attribute("T"))
                .ToArray();

            Assert.Equal(3, geometrySections.Length);
            Assert.Contains("RelCubBezTo", rowTypes);
            Assert.Contains("RelEllipticalArcTo", rowTypes);
            Assert.Contains(geometrySections, section => section.Elements()
                .Any(element => element.Name.LocalName == "Cell"
                    && (string?)element.Attribute("N") == "NoFill"
                    && (string?)element.Attribute("V") == "0"));
        }
        finally
        {
            fixture.Dispose();
            if (File.Exists(output)) File.Delete(output);
        }
    }

    [Trait("Category", "VisioIntegration")]
    [Fact]
    public void Rich_text_character_runs_survive_vsdx_save()
    {
        var fixture = VisioFixture.StartVisible();
        var output = Path.Combine(Path.GetTempPath(), $"visio-rich-text-{Guid.NewGuid():N}.vsdx");
        try
        {
            fixture.AddShape(new AddShapeCommand(
                "equation",
                "text",
                new PixelBox(100, 100, 600, 120),
                RichText: new RichText(
                    "A_l^i = log(|H_l| + ε)",
                    new TextParagraphStyle("center", "middle"),
                    [
                        new TextRun(0, 1, FontFamily: "Arial", FontSizePt: 14, Italic: true, Color: "#0B4A8B"),
                        new TextRun(2, 1, Subscript: true),
                        new TextRun(4, 1, Superscript: true),
                    ])));

            fixture.Save(output);
            fixture.Dispose();

            using var archive = ZipFile.OpenRead(output);
            var page = archive.GetEntry("visio/pages/page1.xml");
            Assert.NotNull(page);
            using var reader = new StreamReader(page.Open());
            var xml = XDocument.Parse(reader.ReadToEnd());
            var positions = xml.Descendants()
                .Where(element => element.Name.LocalName == "Cell" && (string?)element.Attribute("N") == "Pos")
                .Select(element => (string?)element.Attribute("V"))
                .ToArray();
            Assert.Contains("1", positions);
            Assert.Contains("2", positions);
            Assert.Contains(xml.Descendants(), element =>
                element.Name.LocalName == "Cell"
                && (string?)element.Attribute("N") == "Color"
                && string.Equals((string?)element.Attribute("V"), "#0b4a8b", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            fixture.Dispose();
            if (File.Exists(output)) File.Delete(output);
        }
    }

    [Trait("Category", "VisioIntegration")]
    [Fact]
    public void Rich_text_preserves_fractional_points_and_shrink_text_reduces_overflow()
    {
        using var fixture = VisioFixture.StartVisible();
        fixture.AddShape(new AddShapeCommand(
            "fractional",
            "text",
            new PixelBox(100, 100, 240, 80),
            RichText: new RichText(
                "Fractional",
                Runs: [new TextRun(0, 10, FontSizePt: 10.5)],
                Fit: "shrink_text",
                MinimumFontSizePt: 5)));
        fixture.AddShape(new AddShapeCommand(
            "shrink",
            "text",
            new PixelBox(400, 100, 56, 45),
            RichText: new RichText(
                "Dynamic\nresidual",
                Runs: [new TextRun(0, 16, FontSizePt: 12)],
                Fit: "shrink_text",
                MinimumFontSizePt: 5)));

        var fractional = ReadPoints(fixture.GetCells(new GetCellsCommand("fractional", ["Char.Size"])));
        var shrink = ReadPoints(fixture.GetCells(new GetCellsCommand("shrink", ["Char.Size"])));

        Assert.Equal(10.5, fractional, 3);
        Assert.InRange(shrink, 8, 11.99);
    }

    [Trait("Category", "VisioIntegration")]
    [Fact]
    public void Duplicate_semantic_id_returns_stable_error_without_a_partial_shape()
    {
        using var fixture = VisioFixture.StartVisible();
        fixture.AddShape(new AddShapeCommand(
            "encoder",
            "rectangle",
            new PixelBox(100, 100, 240, 100),
            "First"));

        var error = Assert.Throws<VisioTestBridge.Core.BridgeCommandException>(() =>
            fixture.AddShape(new AddShapeCommand(
                "encoder",
                "ellipse",
                new PixelBox(500, 100, 240, 100),
                "Duplicate")));

        Assert.Equal(VisioTestBridge.Core.BridgeErrorCodes.DuplicateSemanticId, error.Code);
        Assert.Single(fixture.Inspect().Shapes);
    }

    [Trait("Category", "VisioIntegration")]
    [Fact]
    public void Add_rectangle_creates_an_editable_semantic_shape()
    {
        using var fixture = VisioFixture.StartVisible();

        var result = fixture.AddShape(new AddShapeCommand(
            "encoder",
            "rectangle",
            new PixelBox(100, 100, 240, 100),
            "Encoder",
            new ShapeStyle(
                "#DAE8FC",
                0,
                "#6C8EBF",
                1.5,
                "#1F2937",
                14,
                0,
                false)));

        Assert.Equal("Codex_encoder", result.NameU);
        Assert.Equal("encoder", result.SemanticId);
        Assert.Equal(1, fixture.GetStatus().ShapeCount);
    }

    private static PathSection WaveSection(double y) => new([
        new ScenePathCommand("M", 20, y),
        new ScenePathCommand("C", 250, y, 70, y - 70, 160, y + 70),
        new ScenePathCommand("C", 500, y, 340, y - 70, 430, y + 70),
    ]);

    private static double ReadPoints(ShapeSheetCellsResult result)
    {
        var value = Assert.Single(result.Cells).ResultUniversal.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
        return double.Parse(value, CultureInfo.InvariantCulture);
    }
}
