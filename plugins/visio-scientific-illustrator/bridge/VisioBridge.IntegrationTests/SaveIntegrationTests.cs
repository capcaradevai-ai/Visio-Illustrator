using VisioBridge.Contracts;

namespace VisioBridge.IntegrationTests;

public sealed class SaveIntegrationTests
{
    [Trait("Category", "VisioIntegration")]
    [Fact]
    public void Save_creates_a_valid_reopenable_vsdx()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "visio-bridge-integration",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var target = Path.Combine(directory, "figure.vsdx");
        try
        {
            using (var fixture = VisioFixture.StartVisible())
            {
                fixture.AddShape(new AddShapeCommand(
                    "saved-shape",
                    "rectangle",
                    new PixelBox(100, 100, 300, 150),
                    "Saved"));
                var result = fixture.Save(target);

                Assert.True(result.Validated);
                Assert.True(result.Bytes > 1000);
                Assert.True(File.Exists(target));
            }
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }
}
