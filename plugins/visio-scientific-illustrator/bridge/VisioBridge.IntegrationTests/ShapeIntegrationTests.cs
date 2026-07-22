using VisioBridge.Contracts;
using System.IO.Compression;
using System.Xml.Linq;

namespace VisioBridge.IntegrationTests;

public sealed class ShapeIntegrationTests
{
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
    public void Duplicate_semantic_id_returns_stable_error_without_a_partial_shape()
    {
        using var fixture = VisioFixture.StartVisible();
        fixture.AddShape(new AddShapeCommand(
            "encoder",
            "rectangle",
            new PixelBox(100, 100, 240, 100),
            "First"));

        var error = Assert.Throws<VisioBridge.Core.BridgeCommandException>(() =>
            fixture.AddShape(new AddShapeCommand(
                "encoder",
                "ellipse",
                new PixelBox(500, 100, 240, 100),
                "Duplicate")));

        Assert.Equal(VisioBridge.Core.BridgeErrorCodes.DuplicateSemanticId, error.Code);
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
}
