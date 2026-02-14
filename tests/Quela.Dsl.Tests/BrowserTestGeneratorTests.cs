namespace Quela.Dsl.Tests;

public class BrowserTestGeneratorTests
{
    [Fact]
    public void GenerateJson_ProducesValidManifest()
    {
        var json = BrowserTestGenerator.GenerateJson();

        Assert.NotEmpty(json);
        Assert.Contains("5.1", json);
        Assert.Contains("filter", json);
        Assert.Contains("forEach", json);

        // Write the manifest to disk for the browser test page
        var outputPath = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "poc", "jsarray", "test-manifest.json");
        File.WriteAllText(outputPath, json);
    }
}
