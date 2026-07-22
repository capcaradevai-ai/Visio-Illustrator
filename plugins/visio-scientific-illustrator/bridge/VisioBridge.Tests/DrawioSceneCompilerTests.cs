using System.IO.Compression;
using System.Text;
using VisioBridge.Contracts;
using VisioBridge.Core.Import;
using VisioBridge.Tests.Fakes;

namespace VisioBridge.Tests;

public sealed class DrawioSceneCompilerTests
{
    private const string Graph = """
        <mxGraphModel pageWidth="800" pageHeight="500"><root>
          <mxCell id="0"/><mxCell id="1" parent="0"/>
          <mxCell id="a" parent="1" vertex="1" value="&lt;b&gt;Input&lt;sub&gt;SD&lt;/sub&gt;&lt;/b&gt;" style="rounded=1;html=1;fillColor=#112233;gradientColor=#DDEEFF;strokeColor=#445566;strokeWidth=2;fontFamily=Arial;fontSize=16;fontStyle=3;rotation=12;"><mxGeometry x="40" y="50" width="180" height="90" as="geometry"/></mxCell>
          <mxCell id="b" parent="1" vertex="1" value="Output" style="ellipse;fillColor=#FFFFFF;strokeColor=#445566;"><mxGeometry x="500" y="50" width="160" height="90" as="geometry"/></mxCell>
          <mxCell id="edge" parent="1" edge="1" source="a" target="b" value="flow" style="edgeStyle=orthogonalEdgeStyle;strokeColor=#445566;strokeWidth=2;endArrow=block;exitX=1;exitY=0.5;entryX=0;entryY=0.5;"><mxGeometry relative="1" as="geometry"><Array as="points"><mxPoint x="340" y="95"/></Array></mxGeometry></mxCell>
        </root></mxGraphModel>
        """;

    [Fact]
    public void Uncompressed_drawio_maps_styles_text_anchors_and_waypoints()
    {
        using var directory = TestDirectory.Create();
        var path = directory.WriteFile("figure.drawio", $"<mxfile><diagram name=\"Page-1\">{Graph}</diagram></mxfile>");

        var result = new DrawioSceneCompiler().Compile(new CompileDrawioSceneCommand(path));

        Assert.Equal(2, result.Statistics.SourceVertexCount);
        Assert.Equal(1, result.Statistics.SourceEdgeCount);
        var input = Assert.IsType<SceneShapeNode>(result.Scene.Nodes.Single(node => node.SemanticId == "a"));
        Assert.Equal("gradient", input.Style?.Fill?.Kind);
        Assert.Equal(12, input.Style?.RotationDeg);
        Assert.Equal("InputSD", input.RichText?.Text);
        Assert.True(input.RichText?.Runs?[0].Bold);
        Assert.True(input.RichText?.Runs?[0].Italic);
        Assert.Contains(input.RichText!.Runs!, run => run.Subscript == true && input.RichText.Text.Substring(run.Start, run.Length) == "SD");
        var connector = Assert.Single(result.Scene.Connectors);
        Assert.Equal("polyline", connector.Routing);
        Assert.Single(connector.Waypoints!);
        Assert.Equal(1, connector.SourceAnchor?.X);
        Assert.Equal(0, connector.TargetAnchor?.X);
    }

    [Fact]
    public void Standard_compressed_diagram_is_decoded()
    {
        using var directory = TestDirectory.Create();
        var path = directory.WriteFile(
            "compressed.drawio",
            $"<mxfile><diagram name=\"Compressed\">{Compress(Graph)}</diagram></mxfile>");

        var result = new DrawioSceneCompiler().Compile(new CompileDrawioSceneCommand(path));

        Assert.Equal("Compressed", result.DiagramName);
        Assert.Equal(2, result.Scene.Nodes.Count);
        Assert.Single(result.Scene.Connectors);
    }

    [Fact]
    public void Invalid_document_returns_stable_error()
    {
        using var directory = TestDirectory.Create();
        var path = directory.WriteFile("bad.drawio", "not xml");

        var error = Assert.Throws<VisioBridge.Core.BridgeCommandException>(() =>
        {
            _ = new DrawioSceneCompiler().Compile(new CompileDrawioSceneCommand(path));
        });

        Assert.Equal(VisioBridge.Core.BridgeErrorCodes.InvalidDrawioDocument, error.Code);
    }

    [Fact]
    public void Nested_stage_panels_do_not_create_extra_top_level_regions()
    {
        using var directory = TestDirectory.Create();
        var graph = """
            <mxGraphModel pageWidth="800" pageHeight="500"><root>
              <mxCell id="0"/><mxCell id="1" parent="0"/>
              <mxCell id="panel_main" parent="1" vertex="1" value="" style="rounded=1;fillColor=#FFFFFF;strokeColor=#000000;"><mxGeometry x="20" y="20" width="760" height="460" as="geometry"/></mxCell>
              <mxCell id="stage_box" parent="1" vertex="1" value="" style="rounded=1;fillColor=#FFFFFF;strokeColor=#000000;"><mxGeometry x="80" y="180" width="620" height="220" as="geometry"/></mxCell>
            </root></mxGraphModel>
            """;
        var path = directory.WriteFile("nested.drawio", $"<mxfile><diagram name=\"Page-1\">{graph}</diagram></mxfile>");

        var result = new DrawioSceneCompiler().Compile(new CompileDrawioSceneCommand(path));

        var region = Assert.Single(result.Scene.Regions);
        Assert.Equal(new PixelBox(20, 20, 760, 460), region.Bounds);
    }

    [Fact]
    public void Invisible_anchor_waveform_compiles_as_editable_path_not_semantic_connector()
    {
        using var directory = TestDirectory.Create();
        var graph = """
            <mxGraphModel pageWidth="400" pageHeight="200"><root>
              <mxCell id="0"/><mxCell id="1" parent="0"/>
              <mxCell id="wave_s" parent="1" vertex="1" value="" style="ellipse;fillColor=none;strokeColor=none;opacity=0;"><mxGeometry x="40" y="80" width="1" height="1" as="geometry"/></mxCell>
              <mxCell id="wave_t" parent="1" vertex="1" value="" style="ellipse;fillColor=none;strokeColor=none;opacity=0;"><mxGeometry x="240" y="80" width="1" height="1" as="geometry"/></mxCell>
              <mxCell id="wave" parent="1" edge="1" source="wave_s" target="wave_t" value="" style="edgeStyle=orthogonalEdgeStyle;curved=1;startArrow=none;endArrow=none;strokeColor=#2F75C4;"><mxGeometry relative="1" as="geometry"><Array as="points"><mxPoint x="100" y="50"/><mxPoint x="180" y="110"/></Array></mxGeometry></mxCell>
            </root></mxGraphModel>
            """;
        var path = directory.WriteFile("wave.drawio", $"<mxfile><diagram name=\"Page-1\">{graph}</diagram></mxfile>");

        var result = new DrawioSceneCompiler().Compile(new CompileDrawioSceneCommand(path));

        var anchor = Assert.IsType<SceneShapeNode>(result.Scene.Nodes.Single(node => node.SemanticId == "wave_s"));
        Assert.Equal("ellipse", anchor.Shape);
        Assert.Empty(result.Scene.Connectors);
        Assert.Contains(result.Scene.Nodes.OfType<ScenePathNode>(), node => node.SemanticId == "wave");
    }

    private static string Compress(string xml)
    {
        var encoded = Encoding.UTF8.GetBytes(Uri.EscapeDataString(xml));
        using var output = new MemoryStream();
        using (var deflate = new DeflateStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
            deflate.Write(encoded);
        return Convert.ToBase64String(output.ToArray());
    }
}
