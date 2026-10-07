using VisioBridge.Contracts;
using System.IO.Compression;
using System.Xml.Linq;

namespace VisioBridge.IntegrationTests;

public sealed class ConnectorIntegrationTests
{
    [Trait("Category", "VisioIntegration")]
    [Fact]
    public void Rich_connector_anchors_jump_label_and_avoidance_survive_vsdx_save()
    {
        var fixture = VisioFixture.StartVisible();
        var output = Path.Combine(Path.GetTempPath(), $"visio-rich-connector-{Guid.NewGuid():N}.vsdx");
        try
        {
            fixture.AddShape(new AddShapeCommand(
                "rich-source", "rectangle", new PixelBox(100, 300, 220, 120)));
            fixture.AddShape(new AddShapeCommand(
                "rich-target", "rectangle", new PixelBox(1100, 300, 220, 120)));
            fixture.AddShape(new AddShapeCommand(
                "obstacle", "rectangle", new PixelBox(650, 260, 260, 220)));

            fixture.AddConnector(new AddConnectorCommand(
                "rich-flow",
                "rich-source",
                "rich-target",
                Label: "feature flow",
                Routing: "right_angle",
                SourceAnchor: new ConnectorAnchor("normalized", X: 1, Y: 0.25),
                TargetAnchor: new ConnectorAnchor("auto", Name: "left"),
                JumpStyle: "arc",
                LabelPosition: 0.7,
                AvoidShapeIds: ["obstacle"]));

            Assert.Equal(0, fixture.ReadCellResult("obstacle", "ShapePermeableX"));
            Assert.Equal(0, fixture.ReadCellResult("obstacle", "ShapePermeableY"));

            fixture.Save(output);
            fixture.Dispose();

            using var archive = ZipFile.OpenRead(output);
            var page = archive.GetEntry("visio/pages/page1.xml");
            Assert.NotNull(page);
            using var reader = new StreamReader(page.Open());
            var xml = XDocument.Parse(reader.ReadToEnd());
            var shapes = xml.Descendants().Where(element => element.Name.LocalName == "Shape").ToArray();
            var connector = Assert.Single(shapes.Where(shape => (string?)shape.Attribute("NameU") == "Codex_rich_flow"));
            var source = Assert.Single(shapes.Where(shape => (string?)shape.Attribute("NameU") == "Codex_rich_source"));
            var target = Assert.Single(shapes.Where(shape => (string?)shape.Attribute("NameU") == "Codex_rich_target"));

            AssertCell(connector, "ConLineJumpCode", "2");
            AssertCell(connector, "ConLineJumpStyle", "1");
            var textPin = connector.Descendants().Single(cell =>
                cell.Name.LocalName == "Cell" && (string?)cell.Attribute("N") == "TxtPinX");
            var width = connector.Descendants().Single(cell =>
                cell.Name.LocalName == "Cell" && (string?)cell.Attribute("N") == "Width");
            var textPinValue = double.Parse((string)textPin.Attribute("V")!, System.Globalization.CultureInfo.InvariantCulture);
            var widthValue = double.Parse((string)width.Attribute("V")!, System.Globalization.CultureInfo.InvariantCulture);
            Assert.Equal(0.7, textPinValue / widthValue, 6);
            Assert.Contains(source.Descendants(), row =>
                row.Name.LocalName == "Row"
                && ((string?)row.Attribute("N"))?.StartsWith("codex_source_", StringComparison.Ordinal) == true);
            Assert.Contains(target.Descendants(), row =>
                row.Name.LocalName == "Row" && (string?)row.Attribute("N") == "left");
        }
        finally
        {
            fixture.Dispose();
            if (File.Exists(output)) File.Delete(output);
        }
    }

    [Trait("Category", "VisioIntegration")]
    [Fact]
    public void Connector_is_glued_to_both_semantic_shapes()
    {
        using var fixture = VisioFixture.StartVisible();
        fixture.AddShape(new AddShapeCommand(
            "source",
            "rectangle",
            new PixelBox(100, 200, 200, 100),
            "Source"));
        fixture.AddShape(new AddShapeCommand(
            "target",
            "rectangle",
            new PixelBox(700, 200, 200, 100),
            "Target"));

        var connector = fixture.AddConnector(new AddConnectorCommand(
            "flow",
            "source",
            "target",
            new ConnectorStyle(
                StrokeColor: "#334155",
                StrokeWidthPt: 1.5,
                EndArrow: "triangle")));

        Assert.Equal(2, connector.ConnectionCount);
        Assert.Equal("Codex_flow", connector.NameU);
        Assert.Equal(1, fixture.GetStatus().ConnectorCount);

        var beforeMove = fixture.Inspect().Shapes.Single(
            shape => shape.SemanticId == "flow");
        fixture.UpdateShape(new UpdateShapeCommand(
            "target",
            X: 900,
            Y: 420));
        var afterMove = fixture.Inspect().Shapes.Single(
            shape => shape.SemanticId == "flow");

        Assert.NotEqual(beforeMove.Bounds, afterMove.Bounds);
        Assert.Equal(
            new[] { "source", "target" },
            afterMove.ConnectedTo.Order(StringComparer.Ordinal));
        Assert.Equal(1, fixture.GetStatus().ConnectorCount);
    }

    [Trait("Category", "VisioIntegration")]
    [Fact]
    public void Polyline_connector_keeps_waypoints_label_and_glued_endpoints()
    {
        using var fixture = VisioFixture.StartVisible();
        fixture.AddShape(new AddShapeCommand(
            "poly-source",
            "rectangle",
            new PixelBox(100, 150, 200, 100),
            "Source"));
        fixture.AddShape(new AddShapeCommand(
            "poly-target",
            "rectangle",
            new PixelBox(1100, 650, 200, 100),
            "Target"));

        var result = fixture.AddConnector(new AddConnectorCommand(
            "poly-flow",
            "poly-source",
            "poly-target",
            Waypoints:
            [
                new PixelPoint(450, 700),
                new PixelPoint(850, 250),
            ],
            Label: "signal",
            Routing: "polyline"));

        var inspected = fixture.Inspect().Shapes.Single(
            shape => shape.SemanticId == "poly-flow");
        Assert.Equal(2, result.ConnectionCount);
        Assert.Equal("signal", inspected.Label);
        Assert.Equal(
            new[] { "poly-source", "poly-target" },
            inspected.ConnectedTo.Order(StringComparer.Ordinal));
        Assert.True(inspected.Bounds.Height > 300);
    }

    [Trait("Category", "VisioIntegration")]
    [Fact]
    public void Polyline_connector_keeps_explicit_geometry_with_named_connection_points()
    {
        using var fixture = VisioFixture.StartVisible();
        fixture.AddShape(new AddShapeCommand(
            "named-source",
            "rectangle",
            new PixelBox(100, 150, 200, 100),
            "Source"));
        fixture.AddShape(new AddShapeCommand(
            "named-target",
            "rectangle",
            new PixelBox(1100, 650, 200, 100),
            "Target"));
        fixture.AddConnectionPoint(new AddConnectionPointCommand(
            "named-source", "lan_1", 1, 0.25));
        fixture.AddConnectionPoint(new AddConnectionPointCommand(
            "named-target", "lan_2", 0, 0.75));

        var result = fixture.AddConnector(new AddConnectorCommand(
            "named-polyline",
            "named-source",
            "named-target",
            Waypoints:
            [
                new PixelPoint(450, 700),
                new PixelPoint(850, 250),
            ],
            Routing: "polyline",
            SourceAnchor: new ConnectorAnchor("connection_point", Name: "lan_1"),
            TargetAnchor: new ConnectorAnchor("connection_point", Name: "lan_2")));

        var sourcePinX = fixture.ReadCellResult("named-source", "PinX");
        var sourcePinY = fixture.ReadCellResult("named-source", "PinY");
        var sourceWidth = fixture.ReadCellResult("named-source", "Width");
        var sourceHeight = fixture.ReadCellResult("named-source", "Height");
        var targetPinX = fixture.ReadCellResult("named-target", "PinX");
        var targetPinY = fixture.ReadCellResult("named-target", "PinY");
        var targetWidth = fixture.ReadCellResult("named-target", "Width");
        var targetHeight = fixture.ReadCellResult("named-target", "Height");

        Assert.Equal(sourcePinX + sourceWidth * 0.5,
            fixture.ReadCellResult("named-polyline", "BeginX"), 6);
        Assert.Equal(sourcePinY + sourceHeight * 0.25,
            fixture.ReadCellResult("named-polyline", "BeginY"), 6);
        Assert.Equal(targetPinX - targetWidth * 0.5,
            fixture.ReadCellResult("named-polyline", "EndX"), 6);
        Assert.Equal(targetPinY - targetHeight * 0.25,
            fixture.ReadCellResult("named-polyline", "EndY"), 6);

        var geometry = fixture.GetGeometry(new GetGeometryCommand("named-polyline"));
        var polylineFormula = Assert.Single(
            geometry.Sections.Single(section => section.Section == 0).Rows
                .SelectMany(row => row.Cells.Values)
                .Where(formula => formula.StartsWith("POLYLINE(", StringComparison.Ordinal)));
        Assert.True(
            polylineFormula.Count(character => character == ',') >= 5,
            polylineFormula);
        Assert.Equal(2, result.ConnectionCount);
        Assert.Equal(
            new[] { "named-source", "named-target" },
            fixture.Inspect().Shapes.Single(shape => shape.SemanticId == "named-polyline")
                .ConnectedTo.Order(StringComparer.Ordinal));
    }

    [Trait("Category", "VisioIntegration")]
    [Fact]
    public void Connector_can_glue_to_explicit_connection_points()
    {
        using var fixture = VisioFixture.StartVisible();
        fixture.AddShape(new AddShapeCommand(
            "point-source",
            "rectangle",
            new PixelBox(100, 250, 200, 100)));
        fixture.AddShape(new AddShapeCommand(
            "point-target",
            "rectangle",
            new PixelBox(800, 250, 200, 100)));
        fixture.AddConnectionPoint("point-source", 1, 0.5);
        fixture.AddConnectionPoint("point-target", 0, 0.5);

        var result = fixture.AddConnector(new AddConnectorCommand(
            "point-flow",
            "point-source",
            "point-target",
            SourceConnectionPoint: 1,
            TargetConnectionPoint: 1));

        Assert.Equal(2, result.ConnectionCount);
    }

    private static void AssertCell(XElement shape, string name, string value)
    {
        Assert.Contains(shape.Descendants(), cell =>
            cell.Name.LocalName == "Cell"
            && (string?)cell.Attribute("N") == name
            && (string?)cell.Attribute("V") == value);
    }
}
